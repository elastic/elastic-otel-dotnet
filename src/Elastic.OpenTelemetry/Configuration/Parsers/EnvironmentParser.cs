// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections;
using Elastic.OpenTelemetry.Configuration.Instrumentations;
using static Elastic.OpenTelemetry.Configuration.EnvironmentVariables;
using static Elastic.OpenTelemetry.Configuration.Parsers.SharedParsers;

namespace Elastic.OpenTelemetry.Configuration.Parsers;

internal class EnvironmentParser(IDictionary environmentVariables)
{
	private string GetSafeEnvironmentVariable(string key)
	{
		var value = environmentVariables.Contains(key) ? environmentVariables[key]?.ToString() : null;
		return value ?? string.Empty;
	}

	internal (bool, HashSet<T>) EnumerateEnabled<T>(bool allEnabled, T[] available, Signals signal, Func<T, string> getter)
	{
		var instrumentations = new HashSet<T>();
		var signalEnv = signal.ToStringFast().ToUpperInvariant();
		var opted = false;
		foreach (var instrumentation in available)
		{
			var name = getter(instrumentation).ToUpperInvariant();
			var key = $"OTEL_DOTNET_AUTO_{signalEnv}_{name}_INSTRUMENTATION_ENABLED";
			var enabled = BoolParser(GetSafeEnvironmentVariable(key));
			if ((enabled.HasValue && enabled.Value) || (!enabled.HasValue && allEnabled))
				instrumentations.Add(instrumentation);
			if (enabled.HasValue)
				opted = true;
		}

		return (opted, instrumentations);
	}

	internal (bool, HashSet<TraceInstrumentation>) EnabledTraceInstrumentations(bool allEnabled) =>
		EnumerateEnabled(allEnabled, TraceInstrumentationExtensions.GetValues(), Signals.Traces, i => i.ToStringFast());

	internal (bool, HashSet<MetricInstrumentation>) EnabledMetricInstrumentations(bool allEnabled) =>
		EnumerateEnabled(allEnabled, MetricInstrumentationExtensions.GetValues(), Signals.Metrics, i => i.ToStringFast());

	internal (bool, HashSet<LogInstrumentation>) EnabledLogInstrumentations(bool allEnabled) =>
		EnumerateEnabled(allEnabled, LogInstrumentationExtensions.GetValues(), Signals.Logs, i => i.ToStringFast());

	public void ParseInstrumentationVariables(
		ConfigCell<Signals?> signalsCell,
		ConfigCell<TraceInstrumentations> tracingCell,
		ConfigCell<MetricInstrumentations> metricsCell,
		ConfigCell<LogInstrumentations> loggingCell)
	{
		var allEnabled = BoolParser(GetSafeEnvironmentVariable(OTEL_DOTNET_AUTO_INSTRUMENTATION_ENABLED));
		var defaultSignals = allEnabled.HasValue
			? allEnabled.Value ? Signals.All : Signals.None
			: Signals.All;

		var logs = BoolParser(GetSafeEnvironmentVariable(OTEL_DOTNET_AUTO_LOGS_INSTRUMENTATION_ENABLED));
		var traces = BoolParser(GetSafeEnvironmentVariable(OTEL_DOTNET_AUTO_TRACES_INSTRUMENTATION_ENABLED));
		var metrics = BoolParser(GetSafeEnvironmentVariable(OTEL_DOTNET_AUTO_METRICS_INSTRUMENTATION_ENABLED));
		// was explicitly configured using environment variables
		bool Configured(bool? source) => source ?? allEnabled ?? true;

		var traceEnabled = Configured(traces);
		var (optedTraces, traceInstrumentations) = EnabledTraceInstrumentations(traceEnabled);
		if (optedTraces)
			tracingCell.Assign(new TraceInstrumentations(traceInstrumentations), ConfigSource.Environment);

		var metricEnabled = Configured(metrics);
		var (optedMetrics, metricInstrumentations) = EnabledMetricInstrumentations(metricEnabled);
		if (optedMetrics)
			metricsCell.Assign(new MetricInstrumentations(metricInstrumentations), ConfigSource.Environment);

		var logEnabled = Configured(logs);
		var (optedLogs, logInstrumentations) = EnabledLogInstrumentations(logEnabled);
		if (optedLogs)
			loggingCell.Assign(new LogInstrumentations(logInstrumentations), ConfigSource.Environment);

		var signals = defaultSignals;

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

		if (logs.HasValue || traces.HasValue || traces.HasValue || allEnabled.HasValue)
			signalsCell.Assign(signals, ConfigSource.Environment);

		if (optedLogs || optedMetrics || optedTraces)
			signalsCell.Assign(signals, ConfigSource.Environment);
	}
}
