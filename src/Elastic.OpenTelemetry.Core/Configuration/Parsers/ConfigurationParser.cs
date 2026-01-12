// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using Elastic.OpenTelemetry.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using static Elastic.OpenTelemetry.Configuration.Parsers.SharedParsers;

namespace Elastic.OpenTelemetry.Configuration.Parsers;

[UnconditionalSuppressMessage("AssemblyLoadTrimming", "IL2026:RequiresUnreferencedCode", Justification = "Manually verified")]
[UnconditionalSuppressMessage("AssemblyLoadTrimming", "IL3050:RequiresDynamicCode", Justification = "Manually verified")]
internal class ConfigurationParser
{
	private readonly IConfiguration _configuration;
	private static readonly string ConfigurationSection = "Elastic:OpenTelemetry";

	internal string? LoggingSectionLogLevel { get; }

	internal ConfigurationParser(IConfiguration configuration)
	{
		_configuration = configuration;

		// This will be used as a fallback if a more specific configuration is not provided.
		// We also store the logging level to use it within the logging event listener to determine the most verbose level to subscribe to.
		LoggingSectionLogLevel = configuration.GetValue<string>($"Logging:LogLevel:{CompositeLogger.LogCategory}");

		// Fall	back to the default logging level if the specific category is not configured.
		if (string.IsNullOrEmpty(LoggingSectionLogLevel))
			LoggingSectionLogLevel = configuration.GetValue<string>("Logging:LogLevel:Default");
	}

	private static void SetFromConfiguration<T>(IConfiguration configuration, ConfigCell<T> cell, Func<string, T?> parser, string? subsection = null)
	{
		//environment configuration takes precedence, assume already configured
		if (cell.Source == ConfigSource.Environment)
			return;

		var fullKey = subsection is null ? $"{ConfigurationSection}:{cell.Key}" : $"{ConfigurationSection}:{subsection}:{cell.Key}";

		var lookup = configuration.GetValue<string>(fullKey);
		if (lookup is null)
			return;

		var parsed = parser(lookup);
		if (parsed is null)
			return;

		cell.AssignFromConfiguration(parsed);
	}

	internal void ParseLogDirectory(ConfigCell<string?> logDirectory) =>
		SetFromConfiguration(_configuration, logDirectory, StringParser);

	internal void ParseLogTargets(ConfigCell<LogTargets?> logTargets) =>
		SetFromConfiguration(_configuration, logTargets, LogTargetsParser);

	internal void ParseLogLevel(ConfigCell<LogLevel?> logLevel)
	{
		SetFromConfiguration(_configuration, logLevel, LogLevelParser);

		if (!string.IsNullOrEmpty(LoggingSectionLogLevel) && logLevel.Source == ConfigSource.Default)
		{
			var level = LogLevelHelpers.ToLogLevel(LoggingSectionLogLevel!);
			logLevel.AssignFromConfiguration(level);
		}
	}

	public void ParseSkipOtlpExporter(ConfigCell<bool?> skipOtlpExporter) =>
		SetFromConfiguration(_configuration, skipOtlpExporter, BoolParser);

	public void ParseSkipInstrumentationAssemblyScanning(ConfigCell<bool?> skipInstrumentationAssemblyScanning) =>
		SetFromConfiguration(_configuration, skipInstrumentationAssemblyScanning, BoolParser);

	internal void ParseOpAmpEndpoint(ConfigCell<string?> opAmpEndpoint) =>
		SetFromConfiguration(_configuration, opAmpEndpoint, StringParser);

	internal void ParseOpAmpHeaders(ConfigCell<string?> opAmpHeaders) =>
		SetFromConfiguration(_configuration, opAmpHeaders, StringParser);

	internal void ParseResourceAttributes(ConfigCell<string?> resourceAttributes)
	{
		var lookup = _configuration.GetValue<string>(EnvironmentVariables.OTEL_RESOURCE_ATTRIBUTES);

		if (lookup is null)
			return;

		var parsed = StringParser(lookup);
		if (parsed is null)
			return;

		resourceAttributes.AssignFromConfiguration(parsed);
	}

	internal static EventLevel LogLevelToEventLevel(LogLevel? eventLogLevel) =>
		eventLogLevel switch
		{
			LogLevel.Trace or LogLevel.Debug => EventLevel.LogAlways,
			LogLevel.Information => EventLevel.Informational,
			LogLevel.Warning => EventLevel.Warning,
			LogLevel.Error => EventLevel.Error,
			LogLevel.Critical => EventLevel.Critical,
			_ => EventLevel.Informational // fallback to info level
		};
}
