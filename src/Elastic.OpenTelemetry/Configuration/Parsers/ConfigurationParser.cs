// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics.Tracing;
using Elastic.OpenTelemetry.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
}
