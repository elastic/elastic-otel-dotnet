// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.IntegrationTests.Helpers;

namespace Elastic.OpenTelemetry.IntegrationTests;

/// <summary>
/// Integration tests that verify the redistributable zip distribution path
/// with auto-instrumentation via the CoreCLR profiler.
/// The test app has no Elastic references — the profiler injects EDOT at runtime.
/// On net8.0+, OpAmp assemblies load via <c>AssemblyLoadContext</c> isolation.
/// </summary>
[Collection("Redistributable")]
public class AutoInstrDistributionTests
{
	private readonly RedistributableFixture _fixture;

	public AutoInstrDistributionTests(RedistributableFixture fixture) => _fixture = fixture;

	private void AssertFixtureReady() =>
		Assert.True(_fixture.IsReady,
			$"Redistributable fixture failed to initialize — test cannot run.\n{_fixture.InitializationError}");

	[Fact(Timeout = 60_000)]
	public async Task AutoInstr_Net8_AlcPath_OpAmpWorks()
	{
		AssertFixtureReady();

		await using var server = new OpAmpTestServer.OpAmpTestServer("""{"log_level":"debug"}""");
		await server.StartAsync();

		var envVars = ProfilerEnvironment.ForCoreCLR(_fixture.InstallationDirectory);
		envVars["ELASTIC_OTEL_OPAMP_ENDPOINT"] = server.Endpoint;
		envVars["OTEL_SERVICE_NAME"] = "autoinstr-net8-opamp-test";

		await using var runner = new TestAppRunner(_fixture.Net8AppPath, envVars);
		await runner.RunToCompletionAsync();

		Assert.Equal(0, runner.ExitCode);
		Assert.Contains("APP_COMPLETE", runner.StandardOutput);
		Assert.NotNull(runner.EdotLogFilePath);

		var analyzer = new EdotLogAnalyzer(runner.EdotLogFilePath);
		analyzer.AssertNoErrors();
		// Confirms ALC path was used (net8.0+ zip distribution)
		analyzer.AssertContainsEventId(102, "UsingIsolatedLoadContext — confirms ALC path");
		// Confirms ALC loaded assemblies
		analyzer.AssertContainsEventId(150, "AssemblyResolved — confirms ALC loaded assemblies");
		analyzer.AssertContainsEventId(106, "OpAmpClientCreated");
		analyzer.AssertContainsEventId(107, "OpAmpClientStarted");
		Assert.True(server.RequestCount >= 1, "Server should have received at least one request.");
	}

	[Fact(Timeout = 60_000)]
	public async Task AutoInstr_Net8_AlcPath_CentralConfigReceived()
	{
		AssertFixtureReady();

		await using var server = new OpAmpTestServer.OpAmpTestServer("""{"log_level":"debug"}""");
		await server.StartAsync();

		var envVars = ProfilerEnvironment.ForCoreCLR(_fixture.InstallationDirectory);
		envVars["ELASTIC_OTEL_OPAMP_ENDPOINT"] = server.Endpoint;
		envVars["OTEL_SERVICE_NAME"] = "autoinstr-net8-config-test";

		await using var runner = new TestAppRunner(_fixture.Net8AppPath, envVars);
		await runner.RunToCompletionAsync();

		Assert.Equal(0, runner.ExitCode);
		Assert.Contains("APP_COMPLETE", runner.StandardOutput);
		Assert.NotNull(runner.EdotLogFilePath);

		var analyzer = new EdotLogAnalyzer(runner.EdotLogFilePath);
		analyzer.AssertNoErrors();
		// Confirms ALC path was used
		analyzer.AssertContainsEventId(102, "UsingIsolatedLoadContext");
		analyzer.AssertContainsEventId(131, "ReceivedInitialCentralConfig");
		analyzer.AssertContainsEventId(200, "ReceivedRemoteConfig");
		analyzer.AssertContainsEventId(205, "ExtractedLogLevel");
	}

	[Fact(Timeout = 60_000)]
	public async Task AutoInstr_Net8_AlcPath_NoOpAmpServer_GracefulFallback()
	{
		AssertFixtureReady();

		var envVars = ProfilerEnvironment.ForCoreCLR(_fixture.InstallationDirectory);
		envVars["ELASTIC_OTEL_OPAMP_ENDPOINT"] = "http://127.0.0.1:1";
		envVars["OTEL_SERVICE_NAME"] = "autoinstr-net8-fallback-test";

		await using var runner = new TestAppRunner(_fixture.Net8AppPath, envVars);
		await runner.RunToCompletionAsync();

		Assert.Equal(0, runner.ExitCode);
		Assert.Contains("APP_COMPLETE", runner.StandardOutput);
		Assert.NotNull(runner.EdotLogFilePath);

		var analyzer = new EdotLogAnalyzer(runner.EdotLogFilePath);
		// Allow EDOT's creation-failed error (EventId 116) and heartbeat errors from the
		// upstream OpenTelemetry.OpAmp.Client library (no EDOT EventId — logged via
		// the library's own ILogger category, observed on net462 but may also appear here)
		analyzer.AssertNoErrors(
			allowedErrorEventIds: [116],
			allowedMessageSubstrings: ["Failed to send heartbeat"]);
		// ALC path was still attempted even though OpAmp server is unreachable
		analyzer.AssertContainsEventId(102, "UsingIsolatedLoadContext — ALC path attempted");
		analyzer.AssertDoesNotContainEventId(131, "Should not receive initial config from unreachable server");
	}
}
