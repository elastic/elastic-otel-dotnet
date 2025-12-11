// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics.CodeAnalysis;
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
			logLevel.Assign(level, ConfigSource.IConfiguration);
		}
	}

	internal void ParseSkipOtlpExporter(ConfigCell<bool?> skipOtlpExporter) =>
		SetFromConfiguration(_configuration, skipOtlpExporter, BoolParser);

	internal void ParseSkipInstrumentationAssemblyScanning(ConfigCell<bool?> skipInstrumentationAssemblyScanning) =>
		SetFromConfiguration(_configuration, skipInstrumentationAssemblyScanning, BoolParser);

	internal void ParseOpAmpEndpoint(ConfigCell<string?> otlpEndpoint) =>
		SetFromConfiguration(_configuration, otlpEndpoint, StringParser);

	internal void ParseResourceAttributes(ConfigCell<string?> resourceAttributes)
	{
		var lookup = _configuration.GetValue<string>(EnvironmentVariables.OTEL_RESOURCE_ATTRIBUTES);

		if (lookup is null)
			return;

		var parsed = StringParser(lookup);
		if (parsed is null)
			return;

		resourceAttributes.Assign(parsed, ConfigSource.IConfiguration);
	}
}
