// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Buffers;
using System.Diagnostics;
using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Core;
using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.Diagnostics;

internal static partial class LoggerMessages
{
	[LoggerMessage(EventId = 1, EventName = "Bootstapped", Level = LogLevel.Information, Message = "Elastic OpenTelemetry bootstrap invoked {newline}{StackTrace}", SkipEnabledCheck = true)]
	public static partial void LogElasticOpenTelemetryBootstrapped(this ILogger logger, string newline, StackTrace stackTrace);

	[LoggerMessage(EventId = 2, EventName = "SharedComponentsCreated", Level = LogLevel.Debug, Message = "Shared components created.")]
	public static partial void LogSharedComponentsCreated(this ILogger logger);

	[LoggerMessage(EventId = 3, EventName = "SharedComponentsReused", Level = LogLevel.Debug, Message = "Reusing existing shared components {newline}{StackTrace}", SkipEnabledCheck = true)]
	public static partial void LogSharedComponentsReused(this ILogger logger, string newline, StackTrace stackTrace);

	[LoggerMessage(EventId = 4, EventName = "ServiceCollectionComponentsReused", Level = LogLevel.Debug, Message = "Reusing existing components on IServiceCollection {newline}{StackTrace}")]
	public static partial void LogComponentsReused(this ILogger logger, string newline, StackTrace stackTrace);

	[LoggerMessage(EventId = 5, EventName = "ConfiguredSignalProvider", Level = LogLevel.Debug, Message = "Configured EDOT defaults for {Signal} via the {Provider}.")]
	public static partial void LogConfiguredSignalProvider(this ILogger logger, string signal, string provider);

	[LoggerMessage(EventId = 6, EventName = "SkippingOtlpExporter", Level = LogLevel.Information, Message = "Skipping OTLP exporter for {Signal} based on the provided `ElasticOpenTelemetryOptions` via the {Provider}.")]
	public static partial void LogSkippingOtlpExporter(this ILogger logger, string signal, string provider);

	[LoggerMessage(EventId = 7, EventName = "LocatedInstrumentationAssembly", Level = LogLevel.Information, Message = "Located {AssemblyFilename} in {Path}.")]
	public static partial void LogLocatedInstrumentationAssembly(this ILogger logger, string assemblyFilename, string path);

	[LoggerMessage(EventId = 8, EventName = "AddedInstrumentation", Level = LogLevel.Information, Message = "Added {InstrumentationName} to {Provider}.")]
	public static partial void LogAddedInstrumentation(this ILogger logger, string instrumentationName, string provider);

	// We explictly reuse the same event ID and this is the same log message, but with different types for the structured data
	[LoggerMessage(EventId = 9, Level = LogLevel.Debug, Message = "{ProcessorName} found `{AttributeName}` attribute with value '{AttributeValue}' on the span.")]
	internal static partial void FoundTag(this ILogger logger, string processorName, string attributeName, string attributeValue);

	// We explictly reuse the same event ID and this is the same log message, but with different types for the structured data
	[LoggerMessage(EventId = 9, Level = LogLevel.Debug, Message = "{ProcessorName} found `{AttributeName}` attribute with value '{AttributeValue}' on the span.")]
	internal static partial void FoundTag(this ILogger logger, string processorName, string attributeName, int attributeValue);

	// We explictly reuse the same event ID and this is the same log message, but with different types for the structured data
	[LoggerMessage(EventId = 10, Level = LogLevel.Debug, Message = "{ProcessorName} set `{AttributeName}` attribute with value '{AttributeValue}' on the span.")]
	internal static partial void SetTag(this ILogger logger, string processorName, string attributeName, string attributeValue);

	// We explictly reuse the same event ID and this is the same log message, but with different types for the structured data
	[LoggerMessage(EventId = 10, Level = LogLevel.Debug, Message = "{ProcessorName} set `{AttributeName}` attribute with value '{AttributeValue}' on the span.")]
	internal static partial void SetTag(this ILogger logger, string processorName, string attributeName, int attributeValue);

	[LoggerMessage(EventId = 11, Level = LogLevel.Debug, Message = "Added '{ProcessorTypeName}' processor to '{BuilderTypeName}'.")]
	public static partial void LogProcessorAdded(this ILogger logger, string processorTypeName, string builderTypeName);

	[LoggerMessage(EventId = 12, Level = LogLevel.Debug, Message = "Added '{MeterName}' meter to '{BuilderTypeName}'.")]
	public static partial void LogMeterAdded(this ILogger logger, string meterName, string builderTypeName);

	[LoggerMessage(EventId = 13, Level = LogLevel.Error, Message = "Unable to configure {BuilderTypeName} with EDOT .NET logging defaults.")]
	public static partial void UnableToConfigureLoggingDefaultsError(this ILogger logger, string builderTypeName);

	public static void LogDistroPreamble(this ILogger logger, SdkActivationMethod activationMethod, ElasticOpenTelemetryComponents components)
	{
		// This occurs once per initialisation, so we don't use `LoggerMessage`s.

		logger.LogInformation("Elastic Distribution of OpenTelemetry (EDOT) .NET: {AgentInformationalVersion}", VersionHelper.InformationalVersion);

		if (components.Logger.LogFileEnabled)
		{
			logger.LogInformation("EDOT log file: {LogFilePath}", components.Logger.LogFilePath);
		}
		else
		{
			logger.LogInformation("EDOT log file: <disabled>");
		}

		logger.LogInformation("Activation method: {ActivationMethod}", activationMethod.ToString());

#if NET8_0
		logger.LogDebug("Matched TFM: {TargetFrameworkMoniker}", "net8.0");
#elif NET9_0
		logger.LogDebug("Matched TFM: {TargetFrameworkMoniker}", "net9.0");
#elif NETSTANDARD2_0
		logger.LogDebug("Matched TFM: {TargetFrameworkMoniker}", "netstandard2.0");
#elif NETSTANDARD2_1
		logger.LogDebug("Matched TFM: {TargetFrameworkMoniker}", "netstandard2.1");
#elif NET462
		logger.LogDebug("Matched TFM: {TargetFrameworkMoniker}", "net462");
#else
		logger.LogDebug("Matched TFM: {TargetFrameworkMoniker}", "<unknown>");
#endif

		try
		{
			var process = Process.GetCurrentProcess();

			logger.LogDebug("Process ID: {ProcessId}", process.Id);
			logger.LogDebug("Process name: {ProcessName}", process.ProcessName);

			logger.LogDebug("Process started: {ProcessStartTime:yyyy-MM-dd HH:mm:ss.fff}", process.StartTime.ToUniversalTime());
		}
		catch
		{
			// GetCurrentProcess can throw PlatformNotSupportedException
		}

#if NET
		logger.LogDebug("Process path: {ProcessPath}", Environment.ProcessPath);
#elif NETSTANDARD
		logger.LogDebug("Process path: {ProcessPath}", "<Not available on .NET Standard>");
#elif NETFRAMEWORK
		logger.LogDebug("Process path: {ProcessPath}", "<Not available on .NET Framework>");
#endif

		logger.LogDebug("Machine name: {MachineName}", Environment.MachineName);
		logger.LogDebug("Process username: {UserName}", Environment.UserName);
		logger.LogDebug("User domain name: {UserDomainName}", Environment.UserDomainName);
		logger.LogDebug("Command current directory: {CurrentDirectory}", Environment.CurrentDirectory);
		logger.LogDebug("Processor count: {ProcessorCount}", Environment.ProcessorCount);
		logger.LogDebug("OS version: {OSVersion}", Environment.OSVersion);
		logger.LogDebug("CLR version: {CLRVersion}", Environment.Version);

		string[] environmentVariables =
		[
			EnvironmentVariables.OTEL_DOTNET_AUTO_LOG_DIRECTORY,
			EnvironmentVariables.OTEL_LOG_LEVEL,
			EnvironmentVariables.ELASTIC_OTEL_LOG_TARGETS,
			EnvironmentVariables.DOTNET_RUNNING_IN_CONTAINER,
			EnvironmentVariables.ELASTIC_OTEL_SKIP_OTLP_EXPORTER,
			EnvironmentVariables.OTEL_EXPORTER_OTLP_ENDPOINT,
			EnvironmentVariables.OTEL_EXPORTER_OTLP_TRACES_ENDPOINT,
			EnvironmentVariables.OTEL_EXPORTER_OTLP_METRICS_ENDPOINT,
			EnvironmentVariables.OTEL_EXPORTER_OTLP_LOGS_ENDPOINT,
			EnvironmentVariables.OTEL_EXPORTER_OTLP_TIMEOUT,
			EnvironmentVariables.OTEL_EXPORTER_OTLP_TRACES_TIMEOUT,
			EnvironmentVariables.OTEL_EXPORTER_OTLP_METRICS_TIMEOUT,
			EnvironmentVariables.OTEL_EXPORTER_OTLP_LOGS_TIMEOUT,
			EnvironmentVariables.OTEL_EXPORTER_OTLP_PROTOCOL,
			EnvironmentVariables.OTEL_EXPORTER_OTLP_TRACES_PROTOCOL,
			EnvironmentVariables.OTEL_EXPORTER_OTLP_METRICS_PROTOCOL,
			EnvironmentVariables.OTEL_EXPORTER_OTLP_LOGS_PROTOCOL,
		];

		foreach (var variable in environmentVariables)
		{
			var envVarValue = Environment.GetEnvironmentVariable(variable);

			if (string.IsNullOrEmpty(envVarValue))
			{
				logger.LogDebug("Environment variable '{EnvironmentVariable}' is not configured.", variable);
			}
			else
			{
				logger.LogDebug("Environment variable '{EnvironmentVariable}' = '{EnvironmentVariableValue}'.", variable, envVarValue);
			}
		}

		// This next set of env vars might include sensitive information, so we redact the values.
		string[] headerEnvironmentVariables =
		[
			EnvironmentVariables.OTEL_EXPORTER_OTLP_HEADERS,
			EnvironmentVariables.OTEL_EXPORTER_OTLP_TRACES_HEADERS,
			EnvironmentVariables.OTEL_EXPORTER_OTLP_METRICS_HEADERS,
			EnvironmentVariables.OTEL_EXPORTER_OTLP_LOGS_HEADERS,
		];

		foreach (var variable in headerEnvironmentVariables)
		{
			var envVarValue = Environment.GetEnvironmentVariable(variable);

			const string redacted = "=<redacted>";

			if (string.IsNullOrEmpty(envVarValue))
			{
				logger.LogDebug("Environment variable '{EnvironmentVariable}' is not configured.", variable);
			}
			else
			{
				var valueSpan = envVarValue.AsSpan();
				var buffer = ArrayPool<char>.Shared.Rent(1024);
				var bufferSpan = buffer.AsSpan();
				var position = 0;
				var count = 0;

				while (true)
				{
					var indexOfComma = valueSpan.IndexOf(',');
					var header = valueSpan.Slice(0, indexOfComma > 0 ? indexOfComma : valueSpan.Length);

					var indexOfEquals = valueSpan.IndexOf('=');

					if (indexOfEquals > 0)
					{
						var key = header.Slice(0, indexOfEquals);
						var value = header.Slice(indexOfEquals + 1);

						if (count++ > 0)
							bufferSpan[position++] = ',';

						key.CopyTo(bufferSpan.Slice(position));
						position += key.Length;
						redacted.AsSpan().CopyTo(bufferSpan.Slice(position));
						position += redacted.Length;
					}

					if (indexOfComma <= 0)
						break;

					valueSpan = valueSpan.Slice(indexOfComma + 1);
				}

				logger.LogDebug("Environment variable '{EnvironmentVariable}' = '{EnvironmentVariableValue}'.", variable, bufferSpan.Slice(0, position).ToString());

				ArrayPool<char>.Shared.Return(buffer);
			}
		}

		components.Options.LogConfigSources(logger);
	}
}
