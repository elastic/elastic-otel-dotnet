// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Diagnostics.Logging;
using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.Diagnostics;

internal static partial class LoggerMessages
{
#pragma warning disable SYSLIB1006 // Multiple logging methods cannot use the same event id within a class
	// We explictly reuse the same event ID and this is the same log message, but with different types for the structured data

	[LoggerMessage(EventId = 100, Level = LogLevel.Trace, Message = "{ProcessorName} found `{AttributeName}` attribute with value '{AttributeValue}' on the span.")]
	internal static partial void FoundTag(this ILogger logger, string processorName, string attributeName, string attributeValue);

	[LoggerMessage(EventId = 100, Level = LogLevel.Trace, Message = "{ProcessorName} found `{AttributeName}` attribute with value '{AttributeValue}' on the span.")]
	internal static partial void FoundTag(this ILogger logger, string processorName, string attributeName, int attributeValue);

	[LoggerMessage(EventId = 101, Level = LogLevel.Trace, Message = "{ProcessorName} set `{AttributeName}` attribute with value '{AttributeValue}' on the span.")]
	internal static partial void SetTag(this ILogger logger, string processorName, string attributeName, string attributeValue);

	[LoggerMessage(EventId = 101, Level = LogLevel.Trace, Message = "{ProcessorName} set `{AttributeName}` attribute with value '{AttributeValue}' on the span.")]
	internal static partial void SetTag(this ILogger logger, string processorName, string attributeName, int attributeValue);
#pragma warning restore SYSLIB1006 // Multiple logging methods cannot use the same event id within a class

	[LoggerMessage(EventId = 20, Level = LogLevel.Trace, Message = "Added '{ProcessorTypeName}' processor to '{BuilderTypeName}'.")]
	public static partial void LogProcessorAdded(this ILogger logger, string processorTypeName, string builderTypeName);

	[LoggerMessage(EventId = 21, Level = LogLevel.Trace, Message = "Added '{MeterName}' meter to '{BuilderTypeName}'.")]
	public static partial void LogMeterAdded(this ILogger logger, string meterName, string builderTypeName);

	public static void LogAgentPreamble(this ILogger logger)
	{
		var process = Process.GetCurrentProcess();
		logger.LogInformation("Elastic OpenTelemetry Distribution: {AgentInformationalVersion}", VersionHelper.InformationalVersion);
		if (logger is CompositeLogger distributionLogger)
		{
			if (distributionLogger.LogFileEnabled)
				logger.LogInformation("Elastic OpenTelemetry Distribution, log file: {LogFilePath}", distributionLogger.LogFilePath);
			else
				logger.LogInformation("Elastic OpenTelemetry Distribution, log file: <disabled>");
		}

		logger.LogInformation("Process ID: {ProcessId}", process.Id);
		logger.LogInformation("Process name: {ProcessName}", process.ProcessName);
#if NET6_0_OR_GREATER
		logger.LogInformation("Process path: {ProcessPath}", Environment.ProcessPath);
#else
		logger.LogInformation("Process path: {ProcessPath}", "<Unknown>");
#endif

		logger.LogInformation("Process started: {ProcessStartTime:yyyy-MM-dd HH:mm:ss.fff}", process.StartTime.ToUniversalTime());
		logger.LogInformation("Machine name: {MachineName}", Environment.MachineName);
		logger.LogInformation("Process username: {UserName}", Environment.UserName);
		logger.LogInformation("User domain name: {UserDomainName}", Environment.UserDomainName);
		// Don't think we should log this for PII purposes?
		//logger.LogInformation("Command line: {ProcessCommandLine}", Environment.CommandLine);
		logger.LogInformation("Command current directory: {CurrentDirectory}", Environment.CurrentDirectory);
		logger.LogInformation("Processor count: {ProcessorCount}", Environment.ProcessorCount);
		logger.LogInformation("OS version: {OSVersion}", Environment.OSVersion);
		logger.LogInformation("CLR version: {CLRVersion}", Environment.Version);

		string[] environmentVariables =
		[
			EnvironmentVariables.ELASTIC_OTEL_LOG_DIRECTORY,
			EnvironmentVariables.ELASTIC_OTEL_LOG_LEVEL
		];

		foreach (var variable in environmentVariables)
		{
			var envVarValue = Environment.GetEnvironmentVariable(variable);

			if (string.IsNullOrEmpty(envVarValue))
				logger.LogDebug("Environment variable '{EnvironmentVariable}' is not configured.", variable);
			else
				logger.LogDebug("Environment variable '{EnvironmentVariable}' = '{EnvironmentVariableValue}'.", variable, envVarValue);
		}
	}
}
