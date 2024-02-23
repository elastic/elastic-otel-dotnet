// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.Diagnostics;

internal sealed class ElasticDiagnosticLoggingObserver(ILogger logger) : IObserver<KeyValuePair<string, object?>>
{
	public void OnNext(KeyValuePair<string, object?> eventData)
	{
		if (eventData.Value is not DiagnosticEvent)
			return;

		switch (eventData.Key)
		{
			case ElasticOpenTelemetryDiagnostics.AgentBuilderInitializingEvent:
				AgentBuilderInitializing(eventData);
				break;

			case ElasticOpenTelemetryDiagnostics.AgentBuilderInitializedEvent:
				AgentBuilderInitialized(eventData);
				break;

			case ElasticOpenTelemetryDiagnostics.AgentBuilderBuiltTracerProviderEvent:
				AgentBuilderBuiltTracerProvider(eventData);
				break;

			case ElasticOpenTelemetryDiagnostics.AgentBuilderBuiltAgentEvent:
				AgentBuilderBuiltAgent(eventData);
				break;

			case ElasticOpenTelemetryDiagnostics.AgentBuildCalledMultipleTimesEvent:
				AgentBuilderBuildCalledMultipleTimes(eventData);
				break;

			case ElasticOpenTelemetryDiagnostics.AgentSetAgentCalledMultipleTimesEvent:
				AgentBuilderSetAgentCalledMultipleTimes(eventData);
				break;

			case ElasticOpenTelemetryDiagnostics.AgentBuilderRegisteredDistroServicesEvent:
				AgentBuilderRegisteredDistroServices(eventData);
				break;

			case ElasticOpenTelemetryDiagnostics.ProcessorAddedEvent:
				ProcessorAdded(eventData);
				break;

			case ElasticOpenTelemetryDiagnostics.SourceAddedEvent:
				SourceAdded(eventData);
				break;

			default:
				if (eventData.Value is DiagnosticEvent diagnostic)
					logger.LogUnhandledEvent(eventData.Key, diagnostic);
				break;
		}

		void AgentBuilderInitializing(KeyValuePair<string, object?> data)
		{
			if (data.Value is DiagnosticEvent diagnostic)
				logger.LogAgentBuilderInitializing(diagnostic);
		}


		void AgentBuilderInitialized(KeyValuePair<string, object?> data)
		{
			if (data.Value is DiagnosticEvent<StackTrace?> diagnostic)
				logger.LogAgentBuilderInitialized(diagnostic);
		}

		void AgentBuilderBuiltTracerProvider(KeyValuePair<string, object?> data)
		{
			if (data.Value is DiagnosticEvent diagnostic)
				logger.LogAgentBuilderBuiltTracerProvider(diagnostic);
		}

		void AgentBuilderBuiltAgent(KeyValuePair<string, object?> data)
		{
			if (data.Value is DiagnosticEvent diagnostic)
				logger.LogAgentBuilderBuiltAgent(diagnostic);
		}

		void AgentBuilderRegisteredDistroServices(KeyValuePair<string, object?> data)
		{
			if (data.Value is DiagnosticEvent diagnostic)
				logger.LogAgentBuilderRegisteredServices(diagnostic);
		}

		void AgentBuilderBuildCalledMultipleTimes(KeyValuePair<string, object?> data)
		{
			if (data.Value is DiagnosticEvent diagnostic)
				logger.LogAgentBuilderBuildCalledMultipleTimes(diagnostic);
		}

		void AgentBuilderSetAgentCalledMultipleTimes(KeyValuePair<string, object?> data)
		{
			if (data.Value is DiagnosticEvent diagnostic)
				logger.LogAgentBuilderSetAgentCalledMultipleTimes(diagnostic);
		}

		void ProcessorAdded(KeyValuePair<string, object?> data)
		{
			if (data.Value is DiagnosticEvent<AddProcessorPayload> diagnostic)
				logger.LogProcessorAdded(diagnostic);
		}

		void SourceAdded(KeyValuePair<string, object?> data)
		{
			if (data.Value is DiagnosticEvent<AddSourcePayload> diagnostic)
				logger.LogSourceAdded(diagnostic);
		}
	}

	public void OnCompleted() { }

	public void OnError(Exception error) { }
}

