// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenTelemetry.OpAmp.Client;
using OpenTelemetry.OpAmp.Client.Listeners;
using OpenTelemetry.OpAmp.Client.Messages;
using OpenTelemetry.OpAmp.Client.Settings;

namespace Elastic.OpenTelemetry.OpAmp.Abstractions;

/// <summary>
/// Internal implementation of OpAmp message subscriber that handles all the complex
/// OpAmp types and marshals them across ALC boundaries using only primitives.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "This assembly only exists in isolated ALC context where dynamic code is supported")]
internal sealed class OpAmpMessageSubscriberImpl : IOpAmpMessageSubscriber, IOpAmpListener<RemoteConfigMessage>
{
	private readonly ILogger _logger;
	private readonly HttpClient _httpClient;
	private OpAmpClient? _client;
	private bool _isConnected;
	private bool _disposed;

	public event Action<string, byte[]>? MessageReceived;
	public event Action<bool>? ConnectionChanged;

	public bool IsConnected => _isConnected;

	public OpAmpMessageSubscriberImpl(ILogger logger)
	{
		_logger = logger;
		_httpClient = new HttpClient();
	}

	public async Task StartAsync(string endpoint, CancellationToken cancellationToken = default)
	{
		try
		{
			_logger.LogDebug("Starting OpAmp message subscriber with endpoint: {Endpoint}", endpoint);

			_client = new OpAmpClient(opts =>
			{
				opts.ServerUrl = new Uri(endpoint);
				opts.ConnectionType = ConnectionType.Http;
				opts.HttpClientFactory = () => _httpClient;
				opts.Identification.AddIdentifyingAttribute("service.name", "elastic-otel");
				opts.Heartbeat.IsEnabled = false;
			});

			// Subscribe to messages - this is the key: we subscribe inside the ALC
			// so the interface type matches the OpAmpClient's expectations
			_client.Subscribe(this);

			await _client.StartAsync(cancellationToken).ConfigureAwait(false);
			_isConnected = true;
			ConnectionChanged?.Invoke(true);

			_logger.LogInformation("OpAmp message subscriber started successfully");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to start OpAmp message subscriber");
			_isConnected = false;
			ConnectionChanged?.Invoke(false);
			throw;
		}
	}

	public async Task StopAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			if (_client != null)
			{
				_logger.LogDebug("Stopping OpAmp message subscriber");
				_client.Dispose();
				_client = null;
				_isConnected = false;
				ConnectionChanged?.Invoke(false);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error stopping OpAmp message subscriber");
		}
	}

	/// <summary>
	/// Implements IOpAmpListener<RemoteConfigMessage> - called by OpAmpClient with messages from the server.
	/// Marshals the message across ALC boundary using only primitives.
	/// </summary>
	public void HandleMessage(RemoteConfigMessage message)
	{
		try
		{
			if (message == null)
			{
				_logger.LogWarning("Received null RemoteConfigMessage");
				return;
			}

			// Serialize the message to JSON bytes for transmission across ALC boundary
			var json = SerializeMessage(message);
			
			_logger.LogDebug("Received RemoteConfigMessage with {AgentConfigCount} configs", 
				message.AgentConfigMap?.Count ?? 0);

			// Raise event with only primitive types - safe across ALC boundaries
			MessageReceived?.Invoke("RemoteConfigMessage", json);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error handling OpAmp message");
		}
	}

	private static byte[] SerializeMessage(RemoteConfigMessage message)
	{
		// Convert the protobuf message to a format that can be serialized to JSON
		var configMap = new Dictionary<string, object>();

		if (message.AgentConfigMap != null)
		{
			foreach (var (key, agentConfig) in message.AgentConfigMap)
			{
				var configObj = new
				{
					contentType = agentConfig.ContentType,
					body = agentConfig.Body != null ? Convert.ToBase64String(agentConfig.Body.ToByteArray()) : null
				};
				configMap[key] = configObj;
			}
		}

		var payload = new
		{
			agentConfigMap = configMap
		};

		// Serialize to JSON bytes using System.Text.Json
		var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(payload, new JsonSerializerOptions { WriteIndented = false });
		return jsonBytes;
	}

	public void Dispose()
	{
		if (_disposed)
			return;

		StopAsync().GetAwaiter().GetResult();
		_httpClient?.Dispose();
		_disposed = true;
	}
}
