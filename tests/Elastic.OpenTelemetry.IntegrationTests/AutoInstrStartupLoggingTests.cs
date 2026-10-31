// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.IntegrationTests.Helpers;

namespace Elastic.OpenTelemetry.IntegrationTests;

/// <summary>
/// Pins the content of the EDOT log for the redistributable (zip) distribution, from plugin
/// load through the first provider build.
/// </summary>
/// <remarks>
/// <para>The plugin bootstraps EDOT logging from its static constructor, before the auto-instrumentation
/// agent builds any provider. From that point, <c>LoggingEventListener</c> forwards the agent's
/// <c>OpenTelemetry-AutoInstrumentation</c> events and the SDK's <c>OpenTelemetry-*</c> events into
/// the EDOT log, after the preamble.</para>
/// <para>These tests act as the parity gate for any move to the OpenTelemetry SDK self-diagnostics,
/// which only starts when a provider is built and would otherwise drop the agent's startup lines.</para>
/// </remarks>
[Collection("Redistributable")]
public class AutoInstrStartupLoggingTests(RedistributableFixture fixture)
{
	private const string AutoInstrumentationEventSource = "OpenTelemetry-AutoInstrumentation";

	// Logged by the agent at Debug while initializing the profiler, before the tracer provider is built.
	// The upstream docs (docs/internal/continuous-profiler.md) name this string as the one to search for.
	private const string AgentStartupMessage = "Continuous profiling configuration:";

	// Logged by the agent immediately after the tracer provider is built.
	private const string AgentTracerInitializedMessage = "OpenTelemetry tracer initialized.";

	private const string PreambleVersionMessage = "Elastic Distribution of OpenTelemetry (EDOT) .NET:";

	private const string PluginTracingMessage =
		"Configuring Elastic Distribution of OpenTelemetry .NET defaults for tracing auto-instrumentation.";

	private readonly RedistributableFixture _fixture = fixture;

	private void AssertFixtureReady() =>
		Assert.True(_fixture.IsReady,
			$"Redistributable fixture failed to initialize — test cannot run.\n{_fixture.InitializationError}");

	private string GetAppPath(string tfm) => tfm switch
	{
		"net8.0" => _fixture.Net8AppPath,
		"net9.0" => _fixture.Net9AppPath,
		"net10.0" => _fixture.Net10AppPath,
		_ => throw new ArgumentOutOfRangeException(nameof(tfm), tfm, "No published app for this TFM.")
	};

	[Theory(Timeout = 90_000)]
	[InlineData("net8.0")]
	[InlineData("net9.0")]
	[InlineData("net10.0")]
	public async Task AutoInstr_StartupLog_ContainsPreambleAndAgentStartupMessages(string tfm)
	{
		AssertFixtureReady();

		var envVars = ProfilerEnvironment.ForCoreCLR(_fixture.InstallationDirectory);
		envVars["OTEL_SERVICE_NAME"] = $"autoinstr-{tfm}-startup-log-test";

		var analyzer = await RunAndAnalyzeAsync(GetAppPath(tfm), envVars);

		AssertStartupLog(analyzer);
	}

	[WindowsOnlyFact(Timeout = 90_000)]
	public async Task AutoInstr_NetFramework_StartupLog_ContainsPreambleAndAgentStartupMessages()
	{
		AssertFixtureReady();

		var envVars = ProfilerEnvironment.ForNetFramework(_fixture.InstallationDirectory);
		envVars["OTEL_SERVICE_NAME"] = "autoinstr-net462-startup-log-test";

		var analyzer = await RunAndAnalyzeAsync(_fixture.Net462AppPath, envVars);

		AssertStartupLog(analyzer);
	}

	private static async Task<EdotLogAnalyzer> RunAndAnalyzeAsync(string appPath, Dictionary<string, string> envVars)
	{
		// The agent does not recognise "trace" (TestAppRunner's default) and falls back to Information,
		// which would suppress its Debug startup messages.
		envVars["OTEL_LOG_LEVEL"] = "debug";

		await using var runner = new TestAppRunner(appPath, envVars);
		await runner.RunToCompletionAsync();

		runner.AssertExitCodeZero();
		Assert.Contains("APP_COMPLETE", runner.StandardOutput);
		Assert.NotNull(runner.EdotLogFilePath);

		// Parse before the runner is disposed, as disposal deletes the log directory.
		return new EdotLogAnalyzer(runner.EdotLogFilePath);
	}

	private static void AssertStartupLog(EdotLogAnalyzer analyzer)
	{
		analyzer.AssertNoErrors();

		// Preamble, written once when the plugin bootstraps EDOT.
		analyzer.AssertMessageOccursOnce(PreambleVersionMessage, "the preamble is written once per process");
		analyzer.AssertContainsMessage("EDOT log file:");
		analyzer.AssertContainsMessage("Activation method: AutoInstrumentation");
		analyzer.AssertContainsMessage("Effective environment variable 'OTEL_DOTNET_AUTO_PLUGINS'",
			"the preamble logs the OTEL_* environment variables");

		// EDOT's own messages from the plugin hooks.
		analyzer.AssertMessageOccursOnce(PluginTracingMessage);
		analyzer.AssertContainsEventId(42, "ActivitySourceAdded — Elastic.Transport added by BeforeConfigureTracerProvider");
		analyzer.AssertContainsEventId(20, "ConfiguredSignalProvider — traces configured via the plugin");
		analyzer.AssertContainsEventId(12, "ConfiguredOtlpExporterOptions — OTLP exporter options configured via the plugin");

		// SDK events forwarded from the OpenTelemetry-Sdk event source.
		Assert.True(analyzer.FromEventSource("OpenTelemetry-Sdk").Count > 0,
			"Expected events forwarded from the 'OpenTelemetry-Sdk' event source.");

		// Agent messages, including those logged before the first provider is built.
		var agentEntries = analyzer.FromEventSource(AutoInstrumentationEventSource);
		Assert.Contains(agentEntries, e => e.Message.Contains(AgentStartupMessage, StringComparison.Ordinal));
		Assert.Contains(agentEntries, e => e.Message.Contains(AgentTracerInitializedMessage, StringComparison.Ordinal));
		analyzer.AssertMessageOccursOnce(AgentStartupMessage, "each agent event is forwarded once");
		analyzer.AssertMessageOccursOnce(AgentTracerInitializedMessage, "each agent event is forwarded once");

		// Order: preamble, then the agent's startup message and EDOT's tracer configuration,
		// then the tracer provider build.
		var preambleIndex = analyzer.IndexOfMessage(PreambleVersionMessage);
		var agentStartupIndex = analyzer.IndexOfMessage(AgentStartupMessage);
		var pluginTracingIndex = analyzer.IndexOfMessage(PluginTracingMessage);
		var tracerInitializedIndex = analyzer.IndexOfMessage(AgentTracerInitializedMessage);

		Assert.True(preambleIndex < agentStartupIndex,
			$"Expected the preamble (line {preambleIndex}) before the agent startup message (line {agentStartupIndex}).");
		Assert.True(agentStartupIndex < tracerInitializedIndex,
			$"Expected the agent startup message (line {agentStartupIndex}) before the tracer was initialized " +
			$"(line {tracerInitializedIndex}); messages logged before the first provider build must be captured.");
		Assert.True(pluginTracingIndex < tracerInitializedIndex,
			$"Expected EDOT's tracer configuration (line {pluginTracingIndex}) before the tracer was initialized " +
			$"(line {tracerInitializedIndex}).");
	}
}
