// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics.Tracing;
using Elastic.OpenTelemetry.Configuration.Instrumentations;
using Elastic.OpenTelemetry.Diagnostics.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using static System.StringSplitOptions;
using static Elastic.OpenTelemetry.Configuration.Parsers.SharedParsers;

namespace Elastic.OpenTelemetry.Configuration.Parsers;

internal class ConfigurationParser
{
	private readonly IConfiguration _configuration;
	private static readonly string ConfigurationSection = "Elastic:OpenTelemetry";

	internal string? LoggingSectionLogLevel { get; }

	public ConfigurationParser(IConfiguration configuration)
	{
		_configuration = configuration;

		// This will be used as a fallback if a more specific configuration is not provided.
		// We also store the logging level to use it within the logging event listener to determine the most verbose level to subscribe to.
		LoggingSectionLogLevel = configuration.GetValue<string>($"Logging:LogLevel:{CompositeLogger.LogCategory}");

		// Fall	back to the default logging level if the specific category is not configured.
		if (string.IsNullOrEmpty(LoggingSectionLogLevel))
			LoggingSectionLogLevel = configuration.GetValue<string>("Logging:LogLevel:Default");
	}


	private static void SetFromConfiguration<T>(IConfiguration configuration, ConfigCell<T> cell, Func<string, T?> parser)
	{
		//environment configuration takes precedence, assume already configured
		if (cell.Source == ConfigSource.Environment)
			return;

		var lookup = configuration.GetValue<string>($"{ConfigurationSection}:{cell.Key}");
		if (lookup is null)
			return;
		var parsed = parser(lookup);
		if (parsed is null)
			return;

		cell.Assign(parsed, ConfigSource.IConfiguration);
	}

	public void ParseLogDirectory(ConfigCell<string?> logDirectory) =>
		SetFromConfiguration(_configuration, logDirectory, StringParser);

	public void ParseLogTargets(ConfigCell<LogTargets?> logTargets) =>
		SetFromConfiguration(_configuration, logTargets, LogTargetsParser);

	public void ParseLogLevel(ConfigCell<LogLevel?> logLevel, ref EventLevel eventLevel)
	{
		SetFromConfiguration(_configuration, logLevel, LogLevelParser);

		if (!string.IsNullOrEmpty(LoggingSectionLogLevel) && logLevel.Source == ConfigSource.Default)
		{
			var level = LogLevelHelpers.ToLogLevel(LoggingSectionLogLevel);
			logLevel.Assign(level, ConfigSource.IConfiguration);
		}

		// this is used to ensure LoggingEventListener matches our log level by using the lowest
		// of our configured loglevel or the default logging section's level.
		var eventLogLevel = logLevel.Value;
		if (!string.IsNullOrEmpty(LoggingSectionLogLevel))
		{
			var sectionLogLevel = LogLevelHelpers.ToLogLevel(LoggingSectionLogLevel) ?? LogLevel.None;

			if (sectionLogLevel < eventLogLevel)
				eventLogLevel = sectionLogLevel;
		}
		eventLevel = eventLogLevel switch
		{
			LogLevel.Trace => EventLevel.Verbose,
			LogLevel.Information => EventLevel.Informational,
			LogLevel.Warning => EventLevel.Warning,
			LogLevel.Error => EventLevel.Error,
			LogLevel.Critical => EventLevel.Critical,
			_ => EventLevel.Informational // fallback to info level
		};

	}

	public void ParseSkipOtlpExporter(ConfigCell<bool?> skipOtlpExporter) =>
		SetFromConfiguration(_configuration, skipOtlpExporter, BoolParser);

	public void ParseSignals(ConfigCell<Signals?> signals) =>
		SetFromConfiguration(_configuration, signals, EnabledSignalsParser);

	public void ParseEnabledDefaults(ConfigCell<ElasticDefaults?> defaults) =>
		SetFromConfiguration(_configuration, defaults, EnabledDefaultsParser);

	public void ParseInstrumentations(
		ConfigCell<Signals?> signals,
		ConfigCell<TraceInstrumentations> tracing,
		ConfigCell<MetricInstrumentations> metrics,
		ConfigCell<LogInstrumentations> logging
	)
	{
		if (tracing.Source != ConfigSource.Environment)
			SetFromConfiguration(_configuration, tracing, ParseTracing);

		if (metrics.Source != ConfigSource.Environment)
			SetFromConfiguration(_configuration, metrics, ParseMetrics);

		if (logging.Source != ConfigSource.Environment)
			SetFromConfiguration(_configuration, logging, ParseLogs);

	}

	private static IEnumerable<T>? ParseInstrumentation<T>(string? config, T[] all, Func<string, Nullable<T>> getter)
		where T : struct
	{
		if (string.IsNullOrWhiteSpace(config))
			return null;

		var toRemove = new HashSet<T>();
		var toAdd = new HashSet<T>();

		foreach (var token in config.Split(new[] { ';', ',' }, RemoveEmptyEntries))
		{
			var candidate = token.Trim();
			var remove = candidate.StartsWith("-");
			candidate = candidate.TrimStart('-');

			var instrumentation = getter(candidate);
			if (!instrumentation.HasValue)
				continue;

			if (remove)
				toRemove.Add(instrumentation.Value);
			else
				toAdd.Add(instrumentation.Value);
		}
		if (toAdd.Count > 0)
			return toAdd;
		if (toRemove.Count > 0)
			return all.Except(toRemove);
		return null;

	}

	private static TraceInstrumentations? ParseTracing(string? tracing)
	{
		var instrumentations = ParseInstrumentation(tracing, TraceInstrumentationExtensions.GetValues(),
			s => TraceInstrumentationExtensions.TryParse(s, out var instrumentation) ? instrumentation : null);
		return instrumentations != null ? new TraceInstrumentations(instrumentations) : null;
	}

	private static MetricInstrumentations? ParseMetrics(string? metrics)
	{
		var instrumentations = ParseInstrumentation(metrics, MetricInstrumentationExtensions.GetValues(),
			s => MetricInstrumentationExtensions.TryParse(s, out var instrumentation) ? instrumentation : null);
		return instrumentations != null ? new MetricInstrumentations(instrumentations) : null;
	}

	private static LogInstrumentations? ParseLogs(string? logs)
	{
		var instrumentations = ParseInstrumentation(logs, LogInstrumentationExtensions.GetValues(),
			s => LogInstrumentationExtensions.TryParse(s, out var instrumentation) ? instrumentation : null);
		return instrumentations != null ? new LogInstrumentations(instrumentations) : null;
	}
}
