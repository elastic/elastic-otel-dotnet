// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.IntegrationTests.Helpers;

namespace Elastic.OpenTelemetry.IntegrationTests;

/// <summary>
/// Integration tests that verify the <c>Elastic.OpenTelemetry.AutoInstrumentation</c>
/// NuGet package works when consumed via PackageReference.
/// </summary>
[Collection("NuGetAutoInstrumentation")]
public class NuGetAutoInstrDistributionTests
{
	private readonly NuGetAutoInstrFixture _fixture;

	public NuGetAutoInstrDistributionTests(NuGetAutoInstrFixture fixture) => _fixture = fixture;

	private void AssertFixtureReady() =>
		Assert.True(_fixture.IsReady,
			$"NuGet AutoInstrumentation fixture failed to initialize — test cannot run.\n{_fixture.InitializationError}");

	private void AssertAotFixtureReady() =>
		Assert.True(_fixture.AotIsReady,
			$"NuGet AutoInstrumentation AOT fixture failed to initialize — test cannot run.\n{_fixture.AotInitializationError}");

	// ── Profiler-based (net10.0) ─────────────────────────────────────────

	[Fact(Timeout = 60_000)]
	public async Task NuGetAutoInstr_Net10_OpAmpWorks()
	{
		AssertFixtureReady();

		await using var server = new OpAmpTestServer.OpAmpTestServer("""{"log_level":"debug"}""");
		await server.StartAsync();

		var envVars = ProfilerEnvironment.ForCoreCLR(_fixture.InstallationDirectory);
		envVars["ELASTIC_OTEL_OPAMP_ENDPOINT"] = server.Endpoint;
		envVars["OTEL_SERVICE_NAME"] = "nuget-autoinstr-net10-opamp-test";

		await using var runner = new TestAppRunner(_fixture.Net10AppPath, envVars);
		await runner.RunToCompletionAsync();

		Assert.Equal(0, runner.ExitCode);
		Assert.Contains("APP_COMPLETE", runner.StandardOutput);
		Assert.NotNull(runner.EdotLogFilePath);

		var analyzer = new EdotLogAnalyzer(runner.EdotLogFilePath);
		analyzer.AssertNoErrors();
		analyzer.AssertContainsEventId(106, "OpAmpClientCreated");
		analyzer.AssertContainsEventId(107, "OpAmpClientStarted");
		Assert.True(server.RequestCount >= 1, "Server should have received at least one request.");
	}

	[Fact(Timeout = 60_000)]
	public async Task NuGetAutoInstr_Net10_CentralConfigReceived()
	{
		AssertFixtureReady();

		await using var server = new OpAmpTestServer.OpAmpTestServer("""{"log_level":"debug"}""");
		await server.StartAsync();

		var envVars = ProfilerEnvironment.ForCoreCLR(_fixture.InstallationDirectory);
		envVars["ELASTIC_OTEL_OPAMP_ENDPOINT"] = server.Endpoint;
		envVars["OTEL_SERVICE_NAME"] = "nuget-autoinstr-net10-config-test";

		await using var runner = new TestAppRunner(_fixture.Net10AppPath, envVars);
		await runner.RunToCompletionAsync();

		Assert.Equal(0, runner.ExitCode);
		Assert.Contains("APP_COMPLETE", runner.StandardOutput);
		Assert.NotNull(runner.EdotLogFilePath);

		var analyzer = new EdotLogAnalyzer(runner.EdotLogFilePath);
		analyzer.AssertNoErrors();
		analyzer.AssertContainsEventId(131, "ReceivedInitialCentralConfig");
		analyzer.AssertContainsEventId(200, "ReceivedRemoteConfig");
		analyzer.AssertContainsEventId(205, "ExtractedLogLevel");
	}

	// ── NativeAOT (net10.0, no profiler) ─────────────────────────────────

	[Fact(Timeout = 60_000)]
	public async Task NuGetAutoInstr_Aot_Net10_BootstrapsAndExitsCleanly()
	{
		AssertAotFixtureReady();

		await using var server = new OpAmpTestServer.OpAmpTestServer("""{"log_level":"debug"}""");
		await server.StartAsync();

		// No profiler env vars — AOT app initializes the plugin directly
		var envVars = new Dictionary<string, string>
		{
			["ELASTIC_OTEL_OPAMP_ENDPOINT"] = server.Endpoint,
			["OTEL_SERVICE_NAME"] = "nuget-autoinstr-aot-test",
		};

		await using var runner = new TestAppRunner(_fixture.AotAppPath, envVars);
		await runner.RunToCompletionAsync();

		Assert.Equal(0, runner.ExitCode);
		Assert.Contains("APP_COMPLETE", runner.StandardOutput);
		Assert.NotNull(runner.EdotLogFilePath);

		var analyzer = new EdotLogAnalyzer(runner.EdotLogFilePath);
		analyzer.AssertNoErrors();
		// In AOT, dynamic code is not supported — ALC path should not be used
		analyzer.AssertDoesNotContainEventId(102, "AOT should not use ALC isolation");
		analyzer.AssertContainsEventId(1, "BootstrapInvoked");
		// Verify OpAmp still works in AOT (non-ALC path)
		analyzer.AssertContainsEventId(106, "OpAmpClientCreated");
		analyzer.AssertContainsEventId(107, "OpAmpClientStarted");
		Assert.True(server.RequestCount >= 1, "Server should have received at least one request.");
	}
}
