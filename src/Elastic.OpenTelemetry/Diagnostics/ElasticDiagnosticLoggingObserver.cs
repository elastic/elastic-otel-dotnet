// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information
using System.Diagnostics;
using Elastic.OpenTelemetry.Extensions;

namespace Elastic.OpenTelemetry.Diagnostics;

internal sealed class ElasticDiagnosticLoggingObserver(LogFileWriter logFileWriter) : IObserver<KeyValuePair<string, object?>>
{
	private readonly LogFileWriter _logFileWriter = logFileWriter;

	public void OnNext(KeyValuePair<string, object?> data)
	{
		if (data.Value is not IDiagnosticEvent)
			return;

		switch (data.Key)
		{
			case ElasticOpenTelemetryDiagnosticSource.AgentBuilderInitializedEvent:
				AgentBuilderInitialized(data);
				break;

			case ElasticOpenTelemetryDiagnosticSource.AgentBuilderBuiltTracerProviderEvent:
				AgentBuilderBuiltTracerProvider(data);
				break;

			case ElasticOpenTelemetryDiagnosticSource.AgentBuilderBuiltAgentEvent:
				AgentBuilderBuiltAgent(data);
				break;

			case ElasticOpenTelemetryDiagnosticSource.AgentBuildCalledMultipleTimesEvent:
				AgentBuilderBuildCalledMultipleTimes(data);
				break;

			case ElasticOpenTelemetryDiagnosticSource.AgentSetAgentCalledMultipleTimesEvent:
				AgentBuilderSetAgentCalledMultipleTimes(data);
				break;

			case ElasticOpenTelemetryDiagnosticSource.AgentBuilderRegisteredDistroServicesEvent:
				AgentBuilderRegisteredDistroServices(data);
				break;

			case ElasticOpenTelemetryDiagnosticSource.TransactionIdAddedEvent:
				TransactionIdAdded(data);
				break;

			case ElasticOpenTelemetryDiagnosticSource.ProcessorAddedEvent:
				ProcessorAdded(data);
				break;

			case ElasticOpenTelemetryDiagnosticSource.SourceAddedEvent:
				SourceAdded(data);
				break;

			default:
				break;
		}

		void AgentBuilderInitialized(KeyValuePair<string, object?> data)
		{
			if (data.Value is DiagnosticEvent<StackTrace?> diagnostic)
				_logFileWriter.LogAgentBuilderInitialized(in diagnostic);
		}

		void AgentBuilderBuiltTracerProvider(KeyValuePair<string, object?> data)
		{
			if (data.Value is DiagnosticEvent diagnostic)
				_logFileWriter.LogAgentBuilderBuiltTracerProvider(in diagnostic);
		}

		void AgentBuilderBuiltAgent(KeyValuePair<string, object?> data)
		{
			if (data.Value is DiagnosticEvent diagnostic)
				_logFileWriter.LogAgentBuilderBuiltAgent(in diagnostic);
		}

		void AgentBuilderRegisteredDistroServices(KeyValuePair<string, object?> data)
		{
			if (data.Value is DiagnosticEvent diagnostic)
				_logFileWriter.LogAgentBuilderRegisteredServices(in diagnostic);
		}

		void AgentBuilderBuildCalledMultipleTimes(KeyValuePair<string, object?> data)
		{
			if (data.Value is DiagnosticEvent diagnostic)
				_logFileWriter.LogAgentBuilderBuildCalledMultipleTimes(in diagnostic);
		}

		void AgentBuilderSetAgentCalledMultipleTimes(KeyValuePair<string, object?> data)
		{
			if (data.Value is DiagnosticEvent diagnostic)
				_logFileWriter.LogAgentBuilderSetAgentCalledMultipleTimes(in diagnostic);
		}

		void TransactionIdAdded(KeyValuePair<string, object?> data)
		{
			if (data.Value is DiagnosticEvent diagnostic)
				_logFileWriter.LogAddedTransactionIdTag(in diagnostic);
		}

		void ProcessorAdded(KeyValuePair<string, object?> data)
		{
			if (data.Value is DiagnosticEvent<AddProcessorEvent> diagnostic)
				_logFileWriter.LogProcessorAdded(in diagnostic);
		}

		void SourceAdded(KeyValuePair<string, object?> data)
		{
			if (data.Value is DiagnosticEvent<AddSourceEvent> diagnostic)
				_logFileWriter.LogSourceAdded(in diagnostic);
		}
	}

	public void OnCompleted() { }

	public void OnError(Exception error) { }
}

