// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.IntegrationTests.Helpers;

namespace Elastic.OpenTelemetry.IntegrationTests;

/// <summary>
/// Integration tests that verify the NuGet package distribution path on net8.0.
/// Tests run against the packed <c>.nupkg</c> consumed by a standalone test app —
/// not project references. This validates the real end-user consumption model.
/// </summary>
/// <remarks>
/// <para>
/// These tests use the hosting/DI path (<c>AddElasticOpenTelemetry()</c>) on net8.0.
/// For .NET Framework (net462) coverage using the manual builder APIs
/// (<c>Sdk.CreateTracerProviderBuilder().WithElasticDefaults()</c>), see
/// <see cref="NuGetNet462DistributionTests"/>.
/// </para>
/// </remarks>
[Collection("NuGetPackage")]
public class NuGetDistributionTests
{
	private readonly NuGetPackageFixture _fixture;

	public NuGetDistributionTests(NuGetPackageFixture fixture) => _fixture = fixture;

	private void AssertFixtureReady() =>
		Assert.True(_fixture.IsReady,
			$"NuGet fixture failed to initialize — test cannot run.\n{_fixture.InitializationError}");

	[SkipOnCiFact(Timeout = 30_000)]
	public async Task NuGet_Net8_DirectPath_OpAmpWorks()
	{
		AssertFixtureReady();

		await using var server = new OpAmpTestServer.OpAmpTestServer("""{"log_level":"debug"}""");
		await server.StartAsync();

		var envVars = new Dictionary<string, string>
		{
			["ELASTIC_OTEL_OPAMP_ENDPOINT"] = server.Endpoint,
			["OTEL_SERVICE_NAME"] = "nuget-net8-opamp-test",
		};

		await using var runner = new TestAppRunner(_fixture.Net8AppPath, envVars);
		await runner.RunToCompletionAsync();

		runner.AssertExitCodeZero();
		Assert.Contains("APP_COMPLETE", runner.StandardOutput);
		Assert.NotNull(runner.EdotLogFilePath);

		var analyzer = new EdotLogAnalyzer(runner.EdotLogFilePath);
		analyzer.AssertNoErrors();
		// No ALC in NuGet builds — OpAmp source compiled directly into the assembly
		analyzer.AssertDoesNotContainEventId(102, "NuGet builds should not use ALC isolation");
		analyzer.AssertContainsEventId(101, "InitializingCentralConfig");
		analyzer.AssertContainsEventId(106, "OpAmpClientCreated");
		analyzer.AssertContainsEventId(107, "OpAmpClientStarted");
		Assert.True(server.RequestCount >= 1, "Server should have received at least one request.");
	}

	[SkipOnCiFact(Timeout = 30_000)]
	public async Task NuGet_Net8_DirectPath_CentralConfigReceived()
	{
		AssertFixtureReady();

		await using var server = new OpAmpTestServer.OpAmpTestServer("""{"log_level":"debug"}""");
		await server.StartAsync();

		var envVars = new Dictionary<string, string>
		{
			["ELASTIC_OTEL_OPAMP_ENDPOINT"] = server.Endpoint,
			["OTEL_SERVICE_NAME"] = "nuget-net8-config-test",
		};

		await using var runner = new TestAppRunner(_fixture.Net8AppPath, envVars);
		await runner.RunToCompletionAsync();

		runner.AssertExitCodeZero();
		Assert.NotNull(runner.EdotLogFilePath);

		var analyzer = new EdotLogAnalyzer(runner.EdotLogFilePath);
		analyzer.AssertNoErrors();
		analyzer.AssertContainsEventId(131, "ReceivedInitialCentralConfig");
		analyzer.AssertContainsEventId(200, "ReceivedRemoteConfig");
		analyzer.AssertContainsEventId(205, "ExtractedLogLevel");
	}

	[SkipOnCiFact(Timeout = 30_000)]
	public async Task NuGet_Net8_DirectPath_NoOpAmpServer_GracefulFallback()
	{
		AssertFixtureReady();

		// Point at a port where nothing is listening
		var envVars = new Dictionary<string, string>
		{
			["ELASTIC_OTEL_OPAMP_ENDPOINT"] = "http://127.0.0.1:1",
			["OTEL_SERVICE_NAME"] = "nuget-net8-fallback-test",
		};

		await using var runner = new TestAppRunner(_fixture.Net8AppPath, envVars);
		await runner.RunToCompletionAsync();

		runner.AssertExitCodeZero();
		Assert.Contains("APP_COMPLETE", runner.StandardOutput);
		Assert.NotNull(runner.EdotLogFilePath);

		var analyzer = new EdotLogAnalyzer(runner.EdotLogFilePath);
		// The client starts and attempts to connect, but never receives config.
		// Allow timeout warnings since the server is unreachable.
		// The upstream OpenTelemetry OpAmp client logs connection failures as Error with no EDOT EventId.
		analyzer.AssertNoErrors(allowedErrorEventIds: [116], allowedMessageSubstrings: ["Failed to send heartbeat message"]); // OpAmpClientCreationFailed
		analyzer.AssertDoesNotContainEventId(131, "Should not receive initial config from unreachable server");
	}
}
