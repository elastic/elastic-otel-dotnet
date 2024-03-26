// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using Elastic.OpenTelemetry.Diagnostics.Logging;
using Elastic.OpenTelemetry.Processors;
using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.Diagnostics;

internal static partial class LoggerMessages
{
	[LoggerMessage(EventId = 100, Level = LogLevel.Trace, Message = $"{nameof(TransactionIdProcessor)} added 'transaction.id' tag to Activity.")]
	internal static partial void TransactionIdProcessorTagAdded(this ILogger logger);

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
			EnvironmentVariables.ElasticOtelLogDirectoryEnvironmentVariable,
			EnvironmentVariables.ElasticOtelLogLevelEnvironmentVariable
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
