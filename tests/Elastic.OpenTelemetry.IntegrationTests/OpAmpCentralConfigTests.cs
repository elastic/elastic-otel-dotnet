// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections;
using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Elastic.OpenTelemetry.IntegrationTests;

public class OpAmpCentralConfigTests
{
	[Fact(Timeout = 15_000)]
	public async Task HappyPath_ReceivesLogLevel()
	{
		await using var server = new OpAmpTestServer.OpAmpTestServer("""{"log_level":"debug"}""");
		await server.StartAsync();

		var options = CreateOptions(server.Endpoint, "test-service");

		using var centralConfig = new CentralConfiguration(options, NullLogger.Instance);

		var received = centralConfig.WaitForFirstConfig(TimeSpan.FromSeconds(3));
		Assert.True(received, "Expected to receive first config from OpAmp server.");

		Assert.True(centralConfig.TryGetInitialConfig(out var config));
		Assert.Equal("debug", config.LogLevel);
		Assert.True(server.RequestCount >= 1, "Server should have received at least one request.");
	}

	[Fact(Timeout = 15_000)]
	public async Task HappyPath_AppliesLogLevelToOptions()
	{
		await using var server = new OpAmpTestServer.OpAmpTestServer("""{"log_level":"debug"}""");
		await server.StartAsync();

		var options = CreateOptions(server.Endpoint, "test-service");

		using var centralConfig = new CentralConfiguration(options, NullLogger.Instance);

		var received = centralConfig.WaitForFirstConfig(TimeSpan.FromSeconds(3));
		Assert.True(received);
		Assert.True(centralConfig.TryGetInitialConfig(out var config));

		options.SetLogLevelFromCentralConfig(config.LogLevel!, NullLogger.Instance);
		Assert.Equal(LogLevel.Debug, options.LogLevel);
	}

	[Fact(Timeout = 15_000)]
	public async Task ServerReturnsNoElasticKey_WaitReturnsFalse()
	{
		await using var server = OpAmpTestServer.OpAmpTestServer.CreateWithEmptyConfigMap();
		await server.StartAsync();

		var options = CreateOptions(server.Endpoint, "test-service");

		using var centralConfig = new CentralConfiguration(options, NullLogger.Instance);

		// The client connects but no "elastic" key is present, so no config is dispatched.
		// WaitForFirstConfig should time out.
		var received = centralConfig.WaitForFirstConfig(TimeSpan.FromSeconds(3));
		Assert.False(received, "Expected WaitForFirstConfig to return false when no 'elastic' key is present.");
	}

	[Fact(Timeout = 15_000)]
	public async Task ServerReturnsNonJsonContentType_WaitReturnsFalse()
	{
		await using var server = new OpAmpTestServer.OpAmpTestServer("""{"log_level":"debug"}""", "text/plain");
		await server.StartAsync();

		var options = CreateOptions(server.Endpoint, "test-service");

		using var centralConfig = new CentralConfiguration(options, NullLogger.Instance);

		// RemoteConfigMessageListener drops entries where ContentType != "application/json".
		var received = centralConfig.WaitForFirstConfig(TimeSpan.FromSeconds(3));
		Assert.False(received, "Expected WaitForFirstConfig to return false for non-JSON content type.");
	}

	[Fact(Timeout = 15_000)]
	public async Task ServerReturnsInvalidLogLevel_ConfigReceivedButLevelUnchanged()
	{
		await using var server = new OpAmpTestServer.OpAmpTestServer("""{"log_level":"banana"}""");
		await server.StartAsync();

		var options = CreateOptions(server.Endpoint, "test-service");

		using var centralConfig = new CentralConfiguration(options, NullLogger.Instance);

		var received = centralConfig.WaitForFirstConfig(TimeSpan.FromSeconds(3));
		Assert.True(received, "Config should still be received even with an invalid log level.");
		Assert.True(centralConfig.TryGetInitialConfig(out var config));
		Assert.Equal("banana", config.LogLevel);

		var initialLogLevel = options.LogLevel;
		options.SetLogLevelFromCentralConfig(config.LogLevel!, NullLogger.Instance);

		// Log level should remain unchanged because "banana" is not a valid level.
		Assert.Equal(initialLogLevel, options.LogLevel);
	}

	[Fact(Timeout = 15_000)]
	public async Task ServerNotReachable_FallsBackGracefully() =>
		// Use an endpoint where nothing is listening.
		// Wrapped in Task.Run because the CentralConfiguration constructor blocks
		// for up to 2s (OpAmp start timeout), and xUnit requires async for Timeout.
		await Task.Run(() =>
		{
			var options = CreateOptions("http://127.0.0.1:1", "test-service");

			// Constructor should complete without throwing, falling back to EmptyOpAmpClient
			// after the OpAmp client start timeout (~2s).
			using var centralConfig = new CentralConfiguration(options, NullLogger.Instance);

			// WaitForFirstConfig returns false immediately when using EmptyOpAmpClient.
			var received = centralConfig.WaitForFirstConfig(TimeSpan.FromSeconds(3));
			Assert.False(received, "Expected WaitForFirstConfig to return false when server is unreachable.");
		});

	[Fact(Timeout = 15_000)]
	public async Task SlowServer_FallsBackAfterStartTimeout()
	{
		// Server accepts the connection but delays 3s before responding —
		// longer than OpAmpBlockingStartTimeoutMilliseconds (2000ms).
		// Unlike ServerNotReachable (TCP-level failure), this tests the
		// timeout path when the server is present but slow.
		await using var server = new OpAmpTestServer.OpAmpTestServer("""{"log_level":"debug"}""");
		server.SetResponseDelay(TimeSpan.FromSeconds(3));
		await server.StartAsync();

		await Task.Run(() =>
		{
			var options = CreateOptions(server.Endpoint, "test-service");

			using var centralConfig = new CentralConfiguration(options, NullLogger.Instance);

			// Client started but the first response didn't arrive within the start timeout,
			// so it fell back to EmptyOpAmpClient.
			var received = centralConfig.WaitForFirstConfig(TimeSpan.FromSeconds(1));
			Assert.False(received, "Expected WaitForFirstConfig to return false when server is too slow.");
		});

		// The server should have received at least one request even though the client timed out.
		Assert.True(server.RequestCount >= 1, "Server should have received the request before the client timed out.");
	}

	[Fact(Timeout = 15_000)]
	public async Task ClientSendsServiceIdentity()
	{
		await using var server = new OpAmpTestServer.OpAmpTestServer("""{"log_level":"debug"}""");
		await server.StartAsync();

		var options = CreateOptions(server.Endpoint, "identity-test-service",
			resourceAttributes: "service.version=1.2.3");

		using var centralConfig = new CentralConfiguration(options, NullLogger.Instance);

		var received = centralConfig.WaitForFirstConfig(TimeSpan.FromSeconds(3));
		Assert.True(received);

		var lastMessage = server.LastReceivedMessage;
		Assert.NotNull(lastMessage);
		Assert.NotNull(lastMessage.AgentDescription);

		var attrs = lastMessage.AgentDescription.IdentifyingAttributes;
		Assert.Contains(attrs, kv => kv.Key == "application.name" && kv.Value.StringValue == "identity-test-service");
		Assert.Contains(attrs, kv => kv.Key == "application.version" && kv.Value.StringValue == "1.2.3");
	}

	[Fact(Timeout = 15_000)]
	public async Task ClientForwardsOpAmpHeaders()
	{
		await using var server = new OpAmpTestServer.OpAmpTestServer("""{"log_level":"debug"}""");
		await server.StartAsync();

		var options = CreateOptions(server.Endpoint, "header-test-service",
			opAmpHeaders: "X-Test-Auth=bearer-token-123,X-Custom=custom-value");

		using var centralConfig = new CentralConfiguration(options, NullLogger.Instance);

		var received = centralConfig.WaitForFirstConfig(TimeSpan.FromSeconds(3));
		Assert.True(received);

		var requests = server.ReceivedRequests;
		Assert.NotEmpty(requests);

		var headers = requests[0].Headers;
		Assert.True(headers.ContainsKey("X-Test-Auth"), "Expected X-Test-Auth header to be forwarded.");
		Assert.Contains("bearer-token-123", headers["X-Test-Auth"]);
		Assert.True(headers.ContainsKey("X-Custom"), "Expected X-Custom header to be forwarded.");
		Assert.Contains("custom-value", headers["X-Custom"]);
	}

	[Fact(Timeout = 15_000)]
	public async Task ExtraJsonProperties_DoNotBreakLogLevelExtraction()
	{
		// Verify forward-compatibility: additional JSON properties beyond "log_level"
		// are tolerated end-to-end through the protobuf → listener → parser → subscriber path.
		await using var server = new OpAmpTestServer.OpAmpTestServer(
			"""{"sampling_rate":0.5,"log_level":"debug","some_future_setting":"value"}""");
		await server.StartAsync();

		var options = CreateOptions(server.Endpoint, "test-service");

		using var centralConfig = new CentralConfiguration(options, NullLogger.Instance);

		var received = centralConfig.WaitForFirstConfig(TimeSpan.FromSeconds(3));
		Assert.True(received, "Config should be received despite extra JSON properties.");
		Assert.True(centralConfig.TryGetInitialConfig(out var config));
		Assert.Equal("debug", config.LogLevel);
	}

	private static CompositeElasticOpenTelemetryOptions CreateOptions(
		string opAmpEndpoint,
		string serviceName,
		string? resourceAttributes = null,
		string? opAmpHeaders = null)
	{
		var envVars = new Hashtable
		{
			[EnvironmentVariables.ELASTIC_OTEL_OPAMP_ENDPOINT] = opAmpEndpoint,
			[EnvironmentVariables.OTEL_SERVICE_NAME] = serviceName,
		};

		if (resourceAttributes is not null)
			envVars[EnvironmentVariables.OTEL_RESOURCE_ATTRIBUTES] = resourceAttributes;

		if (opAmpHeaders is not null)
			envVars[EnvironmentVariables.ELASTIC_OTEL_OPAMP_HEADERS] = opAmpHeaders;

		var options = new CompositeElasticOpenTelemetryOptions(envVars);
		options.ResolveOpAmpServiceIdentity();
		return options;
	}
}
