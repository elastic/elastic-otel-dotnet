// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections;
using System.Text.RegularExpressions;
using static Elastic.OpenTelemetry.Configuration.EnvironmentVariables;
using static Elastic.OpenTelemetry.Configuration.Parsers.ConfigurationParsers;

namespace Elastic.OpenTelemetry.Configuration.Parsers;

internal class InstrumentationParsers(IDictionary environmentVariables)
{
	private static readonly Regex IndividualInstrumentationSwitch =
		new Regex("OTEL_DOTNET_AUTO_(LOGS|TRACES|METRICS)_([^_]+)_INSTRUMENTATION_ENABLED");

	private string GetSafeEnvironmentVariable(string key)
	{
		var value = environmentVariables.Contains(key) ? environmentVariables[key]?.ToString() : null;
		return value ?? string.Empty;
	}

	internal HashSet<T> EnumerateEnabled<T>(bool allEnabled, T[] available, Signals signal, Func<T, string> getter)
	{
		var instrumentations = new HashSet<T>();
		var signalEnv = signal.ToStringFast().ToUpperInvariant();
		foreach (var instrumentation in available)
		{
			var name = getter(instrumentation).ToUpperInvariant();
			var key = $"OTEL_DOTNET_AUTO_{signalEnv}_{name}_INSTRUMENTATION_ENABLED";
			var (configured, enabled) = BoolParser(GetSafeEnvironmentVariable(key));
			if ((enabled.HasValue && enabled.Value) || allEnabled)
				instrumentations.Add(instrumentation);
		}
		return instrumentations;

	}

	internal HashSet<TraceInstrumentation> EnabledTraceInstrumentations(bool allEnabled) =>
		EnumerateEnabled(allEnabled, TraceInstrumentationExtensions.GetValues(), Signals.Traces, i => i.ToStringFast());

	internal HashSet<MetricInstrumentation> EnabledMetricInstrumentations(bool allEnabled) =>
		EnumerateEnabled(allEnabled, MetricInstrumentationExtensions.GetValues(), Signals.Metrics, i => i.ToStringFast());

	internal HashSet<LogInstrumentation> EnabledLogInstrumentations(bool allEnabled) =>
		EnumerateEnabled(allEnabled, LogInstrumentationExtensions.GetValues(), Signals.Logs, i => i.ToStringFast());

	public void Assign(ref Signals? signals, ref ElasticOpenTelemetryOptions.ConfigSource signalsSource)
	{
		var defaultSignals = Signals.All;
		var (succes, allEnabled) = BoolParser(GetSafeEnvironmentVariable(OTEL_DOTNET_AUTO_INSTRUMENTATION_ENABLED));
		if (succes && allEnabled.HasValue)
		{
			defaultSignals = allEnabled.Value ? Signals.All : Signals.None;
			signals = defaultSignals;
			signalsSource = ElasticOpenTelemetryOptions.ConfigSource.Environment;
		}
		var (logsConfigured, logsEnabled) = BoolParser(GetSafeEnvironmentVariable(OTEL_DOTNET_AUTO_LOGS_INSTRUMENTATION_ENABLED));
		var (tracesConfigured, tracesEnabled) = BoolParser(GetSafeEnvironmentVariable(OTEL_DOTNET_AUTO_TRACES_INSTRUMENTATION_ENABLED));
		var (metricsConfigured, metricsEnabled) = BoolParser(GetSafeEnvironmentVariable(OTEL_DOTNET_AUTO_METRICS_INSTRUMENTATION_ENABLED));

		var traceEnabled = allEnabled.HasValue ? allEnabled.Value : tracesEnabled.HasValue ? tracesEnabled.Value : true;
		var traceInstrumentations = EnabledTraceInstrumentations(traceEnabled);

		var metricEnabled = allEnabled.HasValue ? allEnabled.Value : metricsEnabled.HasValue ? metricsEnabled.Value : true;
		var metricInstrumentations = EnabledMetricInstrumentations(metricEnabled);

		var logEnabled = allEnabled.HasValue ? allEnabled.Value : logsEnabled.HasValue ? logsEnabled.Value : true;
		var logInstrumentations = EnabledLogInstrumentations(logEnabled);

		signals = defaultSignals;
		signalsSource = ElasticOpenTelemetryOptions.ConfigSource.Environment;

		if (logInstrumentations.Count > 0)
			signals |= Signals.Logs;
		else
			signals &= ~Signals.Logs;

		if (traceInstrumentations.Count > 0)
			signals |= Signals.Traces;
		else
			signals &= ~Signals.Traces;

		if (metricInstrumentations.Count > 0)
			signals |= Signals.Metrics;
		else
			signals &= ~Signals.Metrics;
	}
}
