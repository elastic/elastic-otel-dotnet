// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information
using System.Diagnostics;
using Elastic.OpenTelemetry.Extensions;
using Elastic.OpenTelemetry.Processors;

namespace Elastic.OpenTelemetry.Diagnostics;

internal static class ElasticOpenTelemetryDiagnosticSource
{
	public const string DiagnosticSourceName = "Elastic.OpenTelemetry";

	internal static readonly DiagnosticSource DiagnosticSource = new DiagnosticListener(DiagnosticSourceName);

	public static void Log(string name)
	{
		if (DiagnosticSource.IsEnabled(name))
			DiagnosticSource.Write(name, new DiagnosticEvent());
	}

	public static void Log(string name, IDiagnosticEvent data)
	{
		if (DiagnosticSource.IsEnabled(name))
			DiagnosticSource.Write(name, data);
	}

	// Events

	public const string AgentBuilderInitializedEvent = "AgentBuilderInitialized";

	public const string AgentBuilderBuiltTracerProviderEvent = "AgentBuilderBuiltTracerProvider";

	public const string AgentBuilderRegisteredDistroServicesEvent = "RegisteredDistroServices";

	public const string AgentBuilderBuiltAgentEvent = "AgentBuilderBuiltAgent";

	public const string TransactionIdAddedEvent = "TransactionIdAdded";

	public const string ProcessorAddedEvent = "ProcessorAdded";

	public const string SourceAddedEvent = "SourceAdded";

	public const string AgentBuildCalledMultipleTimesEvent = "AgentBuildCalledMultipleTimes";

	public const string AgentSetAgentCalledMultipleTimesEvent = "AgentSetAgentCalledMultipleTimes";

	// Log messages

	public const string TransactionIdProcessorTagAddedLog =
		$"{nameof(TransactionIdProcessor)} added 'transaction.id' tag to Activity.";

	public static void LogAgentBuilderInitialized(this LogFileWriter logFileWriter, in DiagnosticEvent<StackTrace?> diagnostic)
	{
		var message = diagnostic.Data is not null
			? $"AgentBuilder initialized{Environment.NewLine}{diagnostic.Data}."
			: "AgentBuilder initialized.";

		logFileWriter.WriteInfoLogLine(diagnostic, message);
	}

	public static void LogAgentBuilderBuiltTracerProvider(this LogFileWriter logFileWriter, in DiagnosticEvent diagnostic) =>
		logFileWriter.WriteInfoLogLine(diagnostic, "AgentBuilder built TracerProvider.");

	public static void LogAgentBuilderBuiltAgent(this LogFileWriter logFileWriter, in DiagnosticEvent diagnostic) =>
		logFileWriter.WriteInfoLogLine(diagnostic, "AgentBuilder built Agent.");

	public static void LogAgentBuilderRegisteredServices(this LogFileWriter logFileWriter, in DiagnosticEvent diagnostic) =>
		logFileWriter.WriteInfoLogLine(diagnostic, "AgentBuilder registered agent services into IServiceCollection.");

	public static void LogAgentBuilderBuildCalledMultipleTimes(this LogFileWriter logFileWriter, in DiagnosticEvent diagnostic) =>
		logFileWriter.WriteErrorLogLine(diagnostic, Agent.BuildErrorMessage);

	public static void LogAgentBuilderSetAgentCalledMultipleTimes(this LogFileWriter logFileWriter, in DiagnosticEvent diagnostic) =>
		logFileWriter.WriteErrorLogLine(diagnostic, Agent.SetAgentErrorMessage);

	public static void LogAddedTransactionIdTag(this LogFileWriter logFileWriter, in DiagnosticEvent diagnostic) =>
		logFileWriter.WriteTraceLogLine(diagnostic, TransactionIdProcessorTagAddedLog);

	public static void LogProcessorAdded(this LogFileWriter logFileWriter, in DiagnosticEvent<AddProcessorEvent> diagnostic)
	{
		var message = $"Added '{diagnostic.Data.ProcessorType}' processor to '{diagnostic.Data.BuilderType.Name}'.";
		logFileWriter.WriteInfoLogLine(diagnostic, message);
	}

	public static void LogSourceAdded(this LogFileWriter logFileWriter, in DiagnosticEvent<AddSourceEvent> diagnostic)
	{
		var message = $"Added '{diagnostic.Data.ActivitySourceName}' ActivitySource to '{diagnostic.Data.BuilderType.Name}'.";
		logFileWriter.WriteInfoLogLine(diagnostic, message);
	}
}
