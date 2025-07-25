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
	// NOTES:
	// - The IDs and EventNames should ideally not change to ensure constistency in log querying.
	// - Avoid using LogLevel.Trace as this level doesn't align well with the upstream diagnostic levels.

	[LoggerMessage(EventId = 1, EventName = "BootstrapInvoked", Level = LogLevel.Debug, Message = "Bootstrap has been invoked {InvocationCount} times.")]
	public static partial void LogBootstrapInvoked(this ILogger logger, int invocationCount);

	[LoggerMessage(EventId = 2, EventName = "ComponentsCreated", Level = LogLevel.Debug, Message = "Elastic OpenTelemetry components created. {newline}{StackTrace}")]
	public static partial void LogComponentsCreated(this ILogger logger, string newline, StackTrace stackTrace);

	[LoggerMessage(EventId = 3, EventName = "SharedComponentsReused", Level = LogLevel.Debug, Message = "Reusing existing shared components.")]
	public static partial void LogSharedComponentsReused(this ILogger logger);

	[LoggerMessage(EventId = 4, EventName = "SharedComponentsNotReused", Level = LogLevel.Debug, Message = "Unable to reuse existing shared components as the provided " +
		"`CompositeElasticOpenTelemetryOptions` do not match.", SkipEnabledCheck = true)]
	public static partial void LogSharedComponentsNotReused(this ILogger logger);

	[LoggerMessage(EventId = 5, EventName = "ServiceCollectionComponentsReused", Level = LogLevel.Debug, Message = "Reusing existing components on IServiceCollection.")]
	public static partial void LogServiceCollectionComponentsReused(this ILogger logger);

	[LoggerMessage(EventId = 6, EventName = "NoExistingComponents", Level = LogLevel.Debug, Message = "No existing components have been found for the {BuilderName} (instance:{BuilderInstanceId}).")]
	public static partial void LogNoExistingComponents(this ILogger logger, string builderName, string builderInstanceId);

	[LoggerMessage(EventId = 7, EventName = "StoringBuilderState", Level = LogLevel.Debug, Message = "Storing state for the current {BuilderName} (instance:{BuilderInstanceId}).")]
	public static partial void LogStoringBuilderState(this ILogger logger, string builderName, string builderInstanceId);

	[LoggerMessage(EventId = 8, EventName = "MultipleWithElasticDefaultsCalls", Level = LogLevel.Warning, Message = "The `WithElasticDefaults` method has been called {CallCount} " +
		"times across all {Target} instances. This method is generally expected to be invoked on a single builder instance. Consider reviewing its usage.")]
	public static partial void LogMultipleWithElasticDefaultsCallsWarning(this ILogger logger, int callCount, string target);

	[LoggerMessage(EventId = 9, EventName = "MultipleAddElasticProcessorsCalls", Level = LogLevel.Warning, Message = "The `AddElasticProcessors` method has been called {CallCount} " +
		"times across all TracerProviderBuilder instances. This method is generally expected to be invoked on a single builder instance. Consider reviewing its usage.")]
	public static partial void LogMultipleAddElasticProcessorsCallsWarning(this ILogger logger, int callCount);

	[LoggerMessage(EventId = 10, EventName = "AddingResourceAttribute", Level = LogLevel.Debug, Message = "Adding resource attribute '{AttributeName}' with value '{AttributeValue}' to " +
		"the ResourceBuilder (instance:{BuilderInstanceId}).")]
	public static partial void LogAddingResourceAttribute(this ILogger logger, string attributeName, string attributeValue, string builderInstanceId);

	[LoggerMessage(EventId = 11, EventName = "ResourceBuilderWithElasticDefaultsMultipleCalls", Level = LogLevel.Debug, Message = "The `WithElasticDefaults` method has been called {CallCount} " +
		"times across all {Target} instances.")]
	public static partial void LogWithElasticDefaultsCallCount(this ILogger logger, int callCount, string target);




	[LoggerMessage(EventId = 20, EventName = "ConfiguredSignalProvider", Level = LogLevel.Debug, Message = "Configured EDOT defaults for {Signal} via the {ProviderBuilderType} (instance:{BuilderInstanceId}).")]
	public static partial void LogConfiguredSignalProvider(this ILogger logger, string signal, string providerBuilderType, string builderInstanceId);

	[LoggerMessage(EventId = 21, EventName = "SkippingOtlpExporter", Level = LogLevel.Information, Message = "Skipping OTLP exporter for {Signal} based on the provided `ElasticOpenTelemetryOptions` " +
		"via the {ProviderBuilderType} (instance:{BuilderInstanceId}).")]
	public static partial void LogSkippingOtlpExporter(this ILogger logger, string signal, string providerBuilderType, string builderInstanceId);

	[LoggerMessage(EventId = 22, EventName = "ProviderBuilderSignalDisabled", Level = LogLevel.Information, Message = "Skipping configuring and setting EDOT defaults for {Signal} on {ProviderBuilderType}" +
		"(instance:{BuilderInstanceId}), as these have been disabled via configuration.")]
	public static partial void LogSignalDisabled(this ILogger logger, string signal, string providerBuilderType, string builderInstanceId);

	[LoggerMessage(EventId = 23, EventName = "SkippingBootstrap", Level = LogLevel.Warning, Message = "Skipping EDOT bootstrap and provider configuration because the `Signals` configuration is set to `None`. " +
		"This likely represents a misconfiguration. If you do not want to use the EDOT for any signals, avoid calling `WithElasticDefaults` on the builder.")]
	public static partial void LogSkippingBootstrapWarning(this ILogger logger);

	[LoggerMessage(EventId = 24, EventName = "BuilderAlreadyConfigured", Level = LogLevel.Debug, Message = "The {ProviderBuilderType} (instance:{BuilderInstanceId}) has already been configured with EDOT defaults.")]
	public static partial void LogBuilderAlreadyConfigured(this ILogger logger, string providerBuilderType, string builderInstanceId);

	[LoggerMessage(EventId = 25, EventName = "ConfiguringBuilder", Level = LogLevel.Information, Message = "Configuring the {ProviderBuilderType} (instance:{BuilderInstanceId}) with EDOT defaults.")]
	public static partial void LogConfiguringBuilder(this ILogger logger, string providerBuilderType, string builderInstanceId);



	[LoggerMessage(EventId = 30, EventName = "LocatedInstrumentationAssembly", Level = LogLevel.Debug, Message = "Located {AssemblyFilename} in {Path}.")]
	public static partial void LogLocatedInstrumentationAssembly(this ILogger logger, string assemblyFilename, string path);

	[LoggerMessage(EventId = 31, EventName = "AddedInstrumentation", Level = LogLevel.Debug, Message = "Added contrib instrumentation '{InstrumentationName}' " +
		"to {ProviderBuilderType} (instance:{BuilderInstanceId}).")]
	public static partial void LogAddedInstrumentation(this ILogger logger, string instrumentationName, string providerBuilderType, string builderInstanceId);

	[LoggerMessage(EventId = 32, EventName = "HttpInstrumentationFound", Level = LogLevel.Debug, Message = "The contrib HTTP instrumentation library was located alongside the executing assembly. " +
		"Skipping adding native {InstrumentationType} instrumentation from the 'System.Net.Http' ActivitySource to '{ProviderBuilderType}' (instance:{BuilderInstanceId}).")]
	public static partial void LogHttpInstrumentationFound(this ILogger logger, string instrumentationType, string providerBuilderType, string builderInstanceId);

	[LoggerMessage(EventId = 33, EventName = "RuntimeInstrumentationFound", Level = LogLevel.Debug, Message = "The contrib runtime instrumentation library was located alongside the executing assembly. " +
		"Skipping adding native metric instrumentation from the 'System.Runtime' ActivitySource.")]
	public static partial void LogRuntimeInstrumentationFound(this ILogger logger);

	[LoggerMessage(EventId = 34, EventName = "AddedInstrumentationViaReflection", Level = LogLevel.Debug, Message = "Added contrib instrumentation '{InstrumentationName}' to " +
		"{ProviderBuilderType} (instance:{BuilderInstanceIdentifier}) via reflection.")]
	public static partial void LogAddedInstrumentationViaReflection(this ILogger logger, string instrumentationName, string providerBuilderType, string builderInstanceIdentifier);

	[LoggerMessage(EventId = 35, EventName = "UnableToFindTypeViaReflection", Level = LogLevel.Debug, Message = "Unable to find {FullyQualifiedTypeName} in {AssemblyFullName}.")]
	public static partial void LogUnableToFindType(this ILogger logger, string fullyQualifiedTypeName, string assemblyFullName);

	[LoggerMessage(EventId = 36, EventName = "UnableToFindMethodViaReflection", Level = LogLevel.Warning, Message = "Unable to find the {FullyQualifiedTypeName}.{MethodName} extension " +
		"method in {AssemblyFullName}.")]
	public static partial void LogUnableToFindMethodWarning(this ILogger logger, string fullyQualifiedTypeName, string methodName, string assemblyFullName);

	[LoggerMessage(EventId = 37, EventName = "InvalidBaseDirectory", Level = LogLevel.Warning, Message = "The result of `AppContext.BaseDirectory` was null or empty. Unable to " +
		"perform instrumentation assembly scanning.")]
	public static partial void LogBaseDirectoryWarning(this ILogger logger);

	[LoggerMessage(EventId = 38, EventName = "SkippingAssemblyScanning", Level = LogLevel.Debug, Message = "Skipping instrumentation assembly scanning on " +
		"{ProviderBuilderType} (instance:{BuilderInstanceId}) because it is disabled in configuration.")]
	public static partial void LogSkippingAssemblyScanning(this ILogger logger, string providerBuilderType, string builderInstanceId);

	[LoggerMessage(EventId = 39, EventName = "AddedResourceDetectorViaReflection", Level = LogLevel.Debug, Message = "Added contrib resource detector '{InstrumentationName}' to " +
	"{ProviderBuilderType} (instance:{BuilderInstanceIdentifier}) via reflection.")]
	public static partial void LogAddedResourceDetectorViaReflection(this ILogger logger, string instrumentationName, string providerBuilderType, string builderInstanceIdentifier);



	[LoggerMessage(EventId = 40, EventName = "ProcessorAdded", Level = LogLevel.Debug, Message = "Added '{ProcessorTypeName}' processor to TracerProviderBuilder (instance:{BuilderInstanceId}).")]
	public static partial void LogProcessorAdded(this ILogger logger, string processorTypeName, string builderInstanceId);

	[LoggerMessage(EventId = 41, EventName = "MeterAdded", Level = LogLevel.Debug, Message = "Added '{MeterName}' meter to MeterProviderBuilder (instance:{BuilderInstanceId}).")]
	public static partial void LogMeterAdded(this ILogger logger, string meterName, string builderInstanceId);

	[LoggerMessage(EventId = 42, EventName = "ActivitySourceAdded", Level = LogLevel.Debug, Message = "Added '{ActivitySource}' to TracerProviderBuilder (instance:{BuilderInstanceId}).")]
	public static partial void LogActivitySourceAdded(this ILogger logger, string activitySource, string builderInstanceId);

	[LoggerMessage(EventId = 43, EventName = "ResourceDetectorAdded", Level = LogLevel.Debug, Message = "Added '{ResourceDetector}' to ResourceBuilder (instance:{BuilderInstanceId}).")]
	public static partial void LogResourceDetectorAdded(this ILogger logger, string resourceDetector, string builderInstanceId);



	[LoggerMessage(EventId = 50, EventName = "FoundTag", Level = LogLevel.Debug, Message = "{ProcessorName} found '{AttributeName}' attribute with value '{AttributeValue}' on the span.")]
	internal static partial void LogFoundTag(this ILogger logger, string processorName, string attributeName, object attributeValue);

	[LoggerMessage(EventId = 51, EventName = "SetTag", Level = LogLevel.Debug, Message = "{ProcessorName} set '{AttributeName}' attribute with value '{AttributeValue}' on the span.")]
	internal static partial void LogSetTag(this ILogger logger, string processorName, string attributeName, object attributeValue);




	[LoggerMessage(EventId = 60, EventName = "DetectedIncludeScopes", Level = LogLevel.Warning, Message = "IncludeScopes is enabled and may cause export issues. See https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/troubleshooting.html#missing-log-records")]
	internal static partial void LogDetectedIncludeScopesWarning(this ILogger logger);



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

		logger.LogDebug("Activation method: {ActivationMethod}", activationMethod.ToString());

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
