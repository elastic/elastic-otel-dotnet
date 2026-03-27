// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections.Concurrent;
using Google.Protobuf;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using OpAmp.Proto.V1;

namespace OpAmpTestServer;

/// <summary>
/// A lightweight OpAmp HTTP test server for integration testing.
/// Accepts protobuf-encoded <c>AgentToServer</c> messages and responds with
/// configurable <c>ServerToAgent</c> messages containing <c>RemoteConfig</c>.
/// </summary>
public sealed class OpAmpTestServer : IAsyncDisposable
{
	private WebApplication? _app;
	private string? _endpoint;
	private int _requestCount;
	private AgentToServer? _lastReceivedMessage;
	private volatile ConfigState _config;
	private long _responseDelayTicks;
	private readonly ConcurrentQueue<ReceivedRequest> _receivedRequests = new();

	public OpAmpTestServer(string elasticConfigJson, string contentType = "application/json") => _config = new ConfigState(elasticConfigJson, contentType, IncludeElasticKey: true);

	private OpAmpTestServer(ConfigState config) => _config = config;

	/// <summary>
	/// The base URL the server is listening on (e.g., "http://127.0.0.1:54321").
	/// Available after <see cref="StartAsync"/>.
	/// </summary>
	public string Endpoint => _endpoint ?? throw new InvalidOperationException("Server not started. Call StartAsync first.");

	/// <summary>
	/// The port the server is listening on. Available after <see cref="StartAsync"/>.
	/// </summary>
	public int Port => new Uri(Endpoint).Port;

	/// <summary>
	/// Number of AgentToServer requests received since start. Thread-safe.
	/// </summary>
	public int RequestCount => Volatile.Read(ref _requestCount);

	/// <summary>
	/// The most recently received <c>AgentToServer</c> message, or null if none received.
	/// </summary>
	public AgentToServer? LastReceivedMessage => Volatile.Read(ref _lastReceivedMessage);

	/// <summary>
	/// All received requests in order, including both the protobuf message and HTTP headers.
	/// </summary>
	public IReadOnlyList<ReceivedRequest> ReceivedRequests => _receivedRequests.ToArray();

	/// <summary>
	/// Updates the config that will be returned on the next request.
	/// </summary>
	public void SetConfig(string elasticConfigJson, string contentType = "application/json") =>
		_config = new ConfigState(elasticConfigJson, contentType, IncludeElasticKey: true);

	/// <summary>
	/// Sets an artificial delay before the server responds. Useful for testing timeout behavior.
	/// </summary>
	public void SetResponseDelay(TimeSpan delay) =>
		Volatile.Write(ref _responseDelayTicks, delay.Ticks);

	/// <summary>
	/// Creates a server that returns no "elastic" key in the config map.
	/// </summary>
	public static OpAmpTestServer CreateWithEmptyConfigMap() =>
		new(new ConfigState(string.Empty, string.Empty, IncludeElasticKey: false));

	/// <summary>
	/// Starts the HTTP listener on a dynamic port, bound to loopback. Idempotent.
	/// </summary>
	public Task StartAsync(CancellationToken cancellationToken = default) =>
		StartAsync("127.0.0.1", cancellationToken);

	/// <summary>
	/// Starts the HTTP listener on a dynamic port, bound to the specified address. Idempotent.
	/// Use <c>"0.0.0.0"</c> to listen on all interfaces (required for Docker-based tests).
	/// </summary>
	public async Task StartAsync(string bindAddress, CancellationToken cancellationToken = default)
	{
		if (_app is not null)
			return;

		var builder = WebApplication.CreateSlimBuilder();
		builder.WebHost.UseUrls($"http://{bindAddress}:0");
		builder.Logging.ClearProviders();

		var app = builder.Build();
		app.MapPost("/", HandleRequestAsync);

		try
		{
			await app.StartAsync(cancellationToken).ConfigureAwait(false);
		}
		catch
		{
			await app.DisposeAsync().ConfigureAwait(false);
			throw;
		}

		_app = app;
		_endpoint = _app.Urls.First();
	}

	private async Task HandleRequestAsync(HttpContext context)
	{
		using var ms = new MemoryStream();
		await context.Request.Body.CopyToAsync(ms).ConfigureAwait(false);
		var agentToServer = AgentToServer.Parser.ParseFrom(ms.ToArray());

		// Capture HTTP headers as a plain dictionary for test assertions.
		var headers = context.Request.Headers
			.ToDictionary(h => h.Key, h => h.Value.ToString());

		_receivedRequests.Enqueue(new ReceivedRequest(agentToServer, headers));
		Volatile.Write(ref _lastReceivedMessage, agentToServer);
		Interlocked.Increment(ref _requestCount);

		// Apply response delay after recording the request but before sending the response.
		// This simulates a server that is slow to respond, not slow to accept.
		var delayTicks = Volatile.Read(ref _responseDelayTicks);
		if (delayTicks > 0)
			await Task.Delay(TimeSpan.FromTicks(delayTicks)).ConfigureAwait(false);

		var snapshot = _config;

		var response = new ServerToAgent
		{
			InstanceUid = agentToServer.InstanceUid
		};

		var configMap = new AgentConfigMap();

		if (snapshot.IncludeElasticKey)
		{
			configMap.ConfigMap.Add("elastic", new AgentConfigFile
			{
				Body = ByteString.CopyFromUtf8(snapshot.ElasticConfigJson),
				ContentType = snapshot.ContentType
			});
		}

		response.RemoteConfig = new AgentRemoteConfig { Config = configMap };

		context.Response.ContentType = "application/x-protobuf";
		var bytes = response.ToByteArray();
		await context.Response.Body.WriteAsync(bytes).ConfigureAwait(false);
	}

	public async ValueTask DisposeAsync()
	{
		var app = Interlocked.Exchange(ref _app, null);
		if (app is not null)
			await app.DisposeAsync().ConfigureAwait(false);
	}

	private sealed record ConfigState(string ElasticConfigJson, string ContentType, bool IncludeElasticKey);

	/// <summary>
	/// Captures both the protobuf message and HTTP headers from a single request.
	/// </summary>
	public sealed record ReceivedRequest(AgentToServer Message, IReadOnlyDictionary<string, string> Headers);
}
