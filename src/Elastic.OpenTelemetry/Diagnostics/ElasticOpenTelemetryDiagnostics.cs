// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information
using System.Diagnostics;
using Elastic.OpenTelemetry.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Elastic.OpenTelemetry.Diagnostics;

internal static class ElasticOpenTelemetryDiagnostics
{
	private static readonly DiagnosticListener Listener = new(DiagnosticSourceName);

	public const string DiagnosticSourceName = "Elastic.OpenTelemetry";

	internal static readonly DiagnosticSource DiagnosticSource = Listener;

	public static IDisposable EnableFileLogging() =>
		Listener.Subscribe(new ElasticDiagnosticLoggingObserver(LogFileWriter.Instance));

	public static void Log(string name)
	{
		if (DiagnosticSource.IsEnabled(name))
			DiagnosticSource.Write(name, new DiagnosticEvent());
	}

	public static void Log(string name, Func<DiagnosticEvent> createDiagnosticEvent)
	{
		// We take a func here so that we only create an instance of the DiagnosticEvent when
		// there is a listener for the event.

		if (DiagnosticSource.IsEnabled(name) && createDiagnosticEvent is not null)
			DiagnosticSource.Write(name, createDiagnosticEvent.Invoke());
	}

	// Events

	public const string AgentBuilderInitializedEvent = "AgentBuilderInitialized";

	public const string AgentBuilderBuiltTracerProviderEvent = "AgentBuilderBuiltTracerProvider";

	public const string AgentBuilderBuiltMeterProviderEvent = "AgentBuilderBuiltMeterProvider";

	public const string AgentBuilderRegisteredDistroServicesEvent = "RegisteredDistroServices";

	public const string AgentBuilderBuiltAgentEvent = "AgentBuilderBuiltAgent";

	public const string TransactionIdAddedEvent = "TransactionIdAdded";

	public const string ProcessorAddedEvent = "ProcessorAdded";

	public const string SourceAddedEvent = "SourceAdded";

	public const string MeterAddedEvent = "MeterAdded";

	public const string AgentBuildCalledMultipleTimesEvent = "AgentBuildCalledMultipleTimes";

	public const string AgentSetAgentCalledMultipleTimesEvent = "AgentSetAgentCalledMultipleTimes";

	public static void LogAgentBuilderInitialized(this LogFileWriter logFileWriter, DiagnosticEvent<StackTrace?> diagnostic)
	{
		var message = diagnostic.Data is not null
			? $"AgentBuilder initialized{Environment.NewLine}{diagnostic.Data}."
			: "AgentBuilder initialized.";

		logFileWriter.WriteInfoLogLine(diagnostic, message);
	}

	public static void LogAgentBuilderBuiltTracerProvider(this LogFileWriter logFileWriter, DiagnosticEvent diagnostic) =>
		logFileWriter.WriteInfoLogLine(diagnostic, "AgentBuilder built TracerProvider.");

	public static void LogAgentBuilderBuiltMeterProvider(this LogFileWriter logFileWriter, DiagnosticEvent diagnostic) =>
	logFileWriter.WriteInfoLogLine(diagnostic, "AgentBuilder built MeterProvider.");

	public static void LogAgentBuilderBuiltAgent(this LogFileWriter logFileWriter, DiagnosticEvent diagnostic) =>
		logFileWriter.WriteInfoLogLine(diagnostic, "AgentBuilder built Agent.");

	public static void LogAgentBuilderRegisteredServices(this LogFileWriter logFileWriter, DiagnosticEvent diagnostic) =>
		logFileWriter.WriteInfoLogLine(diagnostic, "AgentBuilder registered agent services into IServiceCollection.");

	public static void LogAgentBuilderBuildCalledMultipleTimes(this LogFileWriter logFileWriter, DiagnosticEvent diagnostic) =>
		logFileWriter.WriteErrorLogLine(diagnostic, Agent.BuildErrorMessage);

	public static void LogAgentBuilderSetAgentCalledMultipleTimes(this LogFileWriter logFileWriter, DiagnosticEvent diagnostic) =>
		logFileWriter.WriteErrorLogLine(diagnostic, Agent.SetAgentErrorMessage);

	public static void LogAddedTransactionIdTag(this LogFileWriter logFileWriter, DiagnosticEvent diagnostic)
	{
		diagnostic.Logger.TransactionIdProcessorTagAdded();
		logFileWriter.WriteTraceLogLine(diagnostic, LoggerMessages.TransactionIdProcessorTagAddedLog);
	}

	public static void LogProcessorAdded(this LogFileWriter logFileWriter, DiagnosticEvent<AddProcessorPayload> diagnostic)
	{
		var message = $"Added '{diagnostic.Data.ProcessorType}' processor to '{diagnostic.Data.BuilderType.Name}'.";
		logFileWriter.WriteInfoLogLine(diagnostic, message);
	}

	public static void LogSourceAdded(this LogFileWriter logFileWriter, DiagnosticEvent<AddSourcePayload> diagnostic)
	{
		var message = $"Added '{diagnostic.Data.Name}' ActivitySource to '{diagnostic.Data.BuilderType.Name}'.";
		logFileWriter.WriteInfoLogLine(diagnostic, message);
	}

	public static void LogMeterAdded(this LogFileWriter logFileWriter, DiagnosticEvent<AddSourcePayload> diagnostic)
	{
		var message = $"Added '{diagnostic.Data.Name}' Meter to '{diagnostic.Data.BuilderType.Name}'.";
		logFileWriter.WriteInfoLogLine(diagnostic, message);
	}

	public static void LogUnhandledEvent(this LogFileWriter logFileWriter, string eventKey, DiagnosticEvent diagnostic)
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
