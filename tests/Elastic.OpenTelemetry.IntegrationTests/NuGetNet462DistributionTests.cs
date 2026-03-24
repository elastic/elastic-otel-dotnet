// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.IntegrationTests.Helpers;

namespace Elastic.OpenTelemetry.IntegrationTests;

/// <summary>
/// Integration tests that verify the NuGet package distribution path on .NET Framework (net462).
/// Windows only — .NET Framework does not run on Linux.
/// </summary>
[Collection("NuGetPackage")]
public class NuGetNet462DistributionTests
{
	private readonly NuGetPackageFixture _fixture;

	public NuGetNet462DistributionTests(NuGetPackageFixture fixture) => _fixture = fixture;

	private void AssertFixtureReady() =>
		Assert.True(_fixture.Net462IsReady,
			$"NuGet net462 fixture failed to initialize — test cannot run.\n{_fixture.Net462InitializationError}");

	[WindowsOnlyFact(Timeout = 30_000)]
	public async Task NuGet_Net462_DirectPath_OpAmpWorks()
	{
		AssertFixtureReady();

		await using var server = new OpAmpTestServer.OpAmpTestServer("""{"log_level":"debug"}""");
		await server.StartAsync();

		var envVars = new Dictionary<string, string>
		{
			["ELASTIC_OTEL_OPAMP_ENDPOINT"] = server.Endpoint,
			["OTEL_SERVICE_NAME"] = "nuget-net462-opamp-test",
		};

		await using var runner = new TestAppRunner(_fixture.Net462AppPath, envVars);
		await runner.RunToCompletionAsync();

		Assert.Equal(0, runner.ExitCode);
		Assert.Contains("APP_COMPLETE", runner.StandardOutput);
		Assert.NotNull(runner.EdotLogFilePath);

		var analyzer = new EdotLogAnalyzer(runner.EdotLogFilePath);
		analyzer.AssertNoErrors();
		// No ALC on .NET Framework — confirms direct path
		analyzer.AssertDoesNotContainEventId(102, "net462 should not use ALC isolation");
		analyzer.AssertContainsEventId(101, "InitializingCentralConfig");
		analyzer.AssertContainsEventId(106, "OpAmpClientCreated");
		analyzer.AssertContainsEventId(107, "OpAmpClientStarted");
		Assert.True(server.RequestCount >= 1, "Server should have received at least one request.");
	}

	[WindowsOnlyFact(Timeout = 30_000)]
	public async Task NuGet_Net462_DirectPath_CentralConfigReceived()
	{
		AssertFixtureReady();

		await using var server = new OpAmpTestServer.OpAmpTestServer("""{"log_level":"debug"}""");
		await server.StartAsync();

		var envVars = new Dictionary<string, string>
		{
			["ELASTIC_OTEL_OPAMP_ENDPOINT"] = server.Endpoint,
			["OTEL_SERVICE_NAME"] = "nuget-net462-config-test",
		};

		await using var runner = new TestAppRunner(_fixture.Net462AppPath, envVars);
		await runner.RunToCompletionAsync();

		Assert.Equal(0, runner.ExitCode);
		Assert.NotNull(runner.EdotLogFilePath);

		var analyzer = new EdotLogAnalyzer(runner.EdotLogFilePath);
		analyzer.AssertNoErrors();
		analyzer.AssertContainsEventId(131, "ReceivedInitialCentralConfig");
		analyzer.AssertContainsEventId(200, "ReceivedRemoteConfig");
		analyzer.AssertContainsEventId(205, "ExtractedLogLevel");
	}

	[WindowsOnlyFact(Timeout = 30_000)]
	public async Task NuGet_Net462_DirectPath_NoOpAmpServer_GracefulFallback()
	{
		AssertFixtureReady();

		var envVars = new Dictionary<string, string>
		{
			["ELASTIC_OTEL_OPAMP_ENDPOINT"] = "http://127.0.0.1:1",
			["OTEL_SERVICE_NAME"] = "nuget-net462-fallback-test",
		};

		await using var runner = new TestAppRunner(_fixture.Net462AppPath, envVars);
		await runner.RunToCompletionAsync();

		Assert.Equal(0, runner.ExitCode);
		Assert.Contains("APP_COMPLETE", runner.StandardOutput);
		Assert.NotNull(runner.EdotLogFilePath);

		var analyzer = new EdotLogAnalyzer(runner.EdotLogFilePath);
		// Allow EDOT's creation-failed error and upstream OpAmp client heartbeat errors
		// (no EDOT EventId — comes from OpenTelemetry.OpAmp.Client library on net462)
		analyzer.AssertNoErrors(
			allowedErrorEventIds: [116],
			allowedMessageSubstrings: ["Failed to send heartbeat"]);
		analyzer.AssertDoesNotContainEventId(131, "Should not receive initial config from unreachable server");
	}
}
