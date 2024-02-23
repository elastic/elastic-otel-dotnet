// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using System.Text;
using Elastic.OpenTelemetry.DependencyInjection;
using Elastic.OpenTelemetry.Diagnostics.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Elastic.OpenTelemetry.Diagnostics;

internal static class ElasticOpenTelemetryDiagnostics
{
	private static readonly DiagnosticListener Listener = new(DiagnosticSourceName);

	public const string DiagnosticSourceName = "Elastic.OpenTelemetry";

	internal static readonly DiagnosticSource DiagnosticSource = Listener;

	public static IDisposable EnableFileLogging(ILogger logger) =>
		Listener.Subscribe(new ElasticDiagnosticLoggingObserver(logger));

	public static void Log(string name)
	{
		if (DiagnosticSource.IsEnabled(name))
			DiagnosticSource.Write(name, new DiagnosticEvent());
	}

	public static void Log(string name, Func<DiagnosticEvent> createDiagnosticEvent)
	{
		// We take a func here so that we only create an instance of the DiagnosticEvent when
		// there is a listener for the event.

		if (DiagnosticSource.IsEnabled(name))
			DiagnosticSource.Write(name, createDiagnosticEvent.Invoke());
	}

	// Events

	public const string AgentBuilderInitializingEvent = "AgentBuilderInitializing";

	public const string AgentBuilderInitializedEvent = "AgentBuilderInitialized";

	public const string AgentBuilderBuiltTracerProviderEvent = "AgentBuilderBuiltTracerProvider";

	public const string AgentBuilderRegisteredDistroServicesEvent = "RegisteredDistroServices";

	public const string AgentBuilderBuiltAgentEvent = "AgentBuilderBuiltAgent";

	public const string ProcessorAddedEvent = "ProcessorAdded";

	public const string SourceAddedEvent = "SourceAdded";

	public const string AgentBuildCalledMultipleTimesEvent = "AgentBuildCalledMultipleTimes";

	public const string AgentSetAgentCalledMultipleTimesEvent = "AgentSetAgentCalledMultipleTimes";

	public static void LogAgentBuilderInitializing(this ILogger logger, DiagnosticEvent _)
	{
		var process = Process.GetCurrentProcess();
		logger.LogTrace("Elastic OpenTelemetry Distribution: {AgentInformationalVersion}", Agent.InformationalVersion);
		if (logger is AgentCompositeLogger agentLogger)
		{
			if (agentLogger.LogFileEnabled)
				logger.LogDebug("Elastic OpenTelemetry Distribution, log file: {LogFilePath}", agentLogger.LogFilePath);
			else
				logger.LogDebug("Elastic OpenTelemetry Distribution, log file: <disabled>");
		}

		logger.LogDebug("Process ID: {ProcessId}", process.Id);
		logger.LogDebug("Process name: {ProcessName}", process.ProcessName);
		logger.LogDebug("Process path: {ProcessPath}", Environment.ProcessPath);

		logger.LogDebug("Process started: {ProcessStartTime:yyyy-MM-dd HH:mm:ss.fff}", process.StartTime.ToUniversalTime());
		logger.LogDebug("Machine name: {MachineName}", Environment.MachineName);
		logger.LogDebug("Process username: {UserName}", Environment.UserName);
		logger.LogDebug("User domain name: {UserDomainName}", Environment.UserDomainName);
		logger.LogDebug("Command line: {ProcessCommandLine}", Environment.CommandLine);
		logger.LogDebug("Command current directory: {CurrentDirectory}", Environment.CurrentDirectory);
		logger.LogDebug("Processor count: {ProcessorCount}", Environment.ProcessorCount);
		logger.LogDebug("OS version: {OSVersion}", Environment.OSVersion);
		logger.LogDebug("CLR version: {CLRVersion}", Environment.Version);

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

	public static void LogAgentBuilderInitialized(this ILogger logger, DiagnosticEvent<StackTrace?> diagnostic)
	{
		var message = diagnostic.Data is not null
			? $"AgentBuilder initialized{Environment.NewLine}{diagnostic.Data}."
			: "AgentBuilder initialized.";

		logger.WriteInfoLogLine(diagnostic, message);
	}

	public static void LogAgentBuilderBuiltTracerProvider(this ILogger logger, DiagnosticEvent diagnostic) =>
		logger.WriteInfoLogLine(diagnostic, "AgentBuilder built TracerProvider.");

	public static void LogAgentBuilderBuiltAgent(this ILogger logger, DiagnosticEvent diagnostic) =>
		logger.WriteInfoLogLine(diagnostic, "AgentBuilder built Agent.");

	public static void LogAgentBuilderRegisteredServices(this ILogger logger, DiagnosticEvent diagnostic) =>
		logger.WriteInfoLogLine(diagnostic, "AgentBuilder registered agent services into IServiceCollection.");

	public static void LogAgentBuilderBuildCalledMultipleTimes(this ILogger logger, DiagnosticEvent diagnostic) =>
		logger.WriteErrorLogLine(diagnostic, Agent.BuildErrorMessage);

	public static void LogAgentBuilderSetAgentCalledMultipleTimes(this ILogger logger, DiagnosticEvent diagnostic) =>
		logger.WriteErrorLogLine(diagnostic, Agent.SetAgentErrorMessage);

	public static void LogProcessorAdded(this ILogger logger, DiagnosticEvent<AddProcessorPayload> diagnostic)
	{
		var message = $"Added '{diagnostic.Data.ProcessorType}' processor to '{diagnostic.Data.BuilderType.Name}'.";
		logger.WriteInfoLogLine(diagnostic, message);
	}

	public static void LogSourceAdded(this ILogger logger, DiagnosticEvent<AddSourcePayload> diagnostic)
	{
		var message = $"Added '{diagnostic.Data.ActivitySourceName}' ActivitySource to '{diagnostic.Data.BuilderType.Name}'.";
		logger.WriteInfoLogLine(diagnostic, message);
	}

	public static void LogUnhandledEvent(this ILogger logFileWriter, string eventKey, DiagnosticEvent diagnostic)
	{
		// Prefer the logger from the source of the event, when present, otherwise
		// fallback to using a logger typed to the ElasticDiagnosticLoggingObserver instead.

		var logger = diagnostic.Logger;

		if (logger == NullLogger.Instance)
			logger = LoggerResolver.GetLogger<ElasticDiagnosticLoggingObserver>();

		logger.UnhandledDiagnosticEvent(eventKey);

		logFileWriter.WriteWarningLogLine(diagnostic, $"Received an unhandled diagnostic event '{eventKey}'.");
	}
}
