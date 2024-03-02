// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using Elastic.OpenTelemetry.Diagnostics.Logging;
using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.Diagnostics;

internal static class ElasticOpenTelemetryDiagnostics
{
	public static void LogAgentPreamble(this ILogger logger)
	{
		var process = Process.GetCurrentProcess();
		logger.LogInformation("Elastic OpenTelemetry Distribution: {AgentInformationalVersion}", Agent.InformationalVersion);
		if (logger is AgentCompositeLogger agentLogger)
		{
			if (agentLogger.LogFileEnabled)
				logger.LogInformation("Elastic OpenTelemetry Distribution, log file: {LogFilePath}", agentLogger.LogFilePath);
			else
				logger.LogInformation("Elastic OpenTelemetry Distribution, log file: <disabled>");
		}

		logger.LogInformation("Process ID: {ProcessId}", process.Id);
		logger.LogInformation("Process name: {ProcessName}", process.ProcessName);
		logger.LogInformation("Process path: {ProcessPath}", Environment.ProcessPath);

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

	public static void LogAgentBuilderInitialized(this ILogger logger, StackTrace stackTrace) =>
		logger.LogInformation($"AgentBuilder initialized{Environment.NewLine}{{StackTrace}}.", stackTrace);

	public static void LogAgentBuilderBuiltTracerProvider(this ILogger logger) =>
		logger.LogInformation("AgentBuilder built TracerProvider.");

	public static void LogAgentBuilderBuiltAgent(this ILogger logger) =>
		logger.LogInformation("AgentBuilder built Agent.");

	public static void LogAgentBuilderRegisteredServices(this ILogger logger) =>
		logger.LogInformation("AgentBuilder registered agent services into IServiceCollection.");

	public static void LogProcessorAdded(this ILogger logger, Type processorType, Type builderType)
	{
		var message = $"Added '{processorType}' processor to '{builderType.Name}'.";
		logger.LogInformation(message);
	}

	public static void LogSourceAdded(this ILogger logger, string activitySourceName, Type builderType)
	{
		var message = $"Added '{activitySourceName}' ActivitySource to '{builderType.Name}'.";
		logger.LogInformation(message);
	}

	public static void LogUnhandledEvent(this ILogger logger, string eventKey) =>
		logger.UnhandledDiagnosticEvent(eventKey);
}
