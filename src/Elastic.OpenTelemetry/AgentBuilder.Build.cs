// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Diagnostics;
using Elastic.OpenTelemetry.Diagnostics.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Elastic.OpenTelemetry;

/// <summary> TODO </summary>
public static class OpenTelemetryBuilderExtensions
{
	/// <summary>
	/// Build an instance of <see cref="IAgent"/>.
	/// </summary>
	/// <returns>A new instance of <see cref="IAgent"/>.</returns>
	public static IAgent Build(this IOpenTelemetryBuilder builder, ILogger? logger = null, IServiceProvider? serviceProvider = null)
	{
		// this happens if someone calls Build() while using IServiceCollection and AddOpenTelemetry() and NOT Add*Elastic*OpenTelemetry()
		// we treat this a NOOP
		// NOTE for AddElasticOpenTelemetry(this IServiceCollection services) calling Build() manually is NOT required.
		if (builder is not AgentBuilder agentBuilder)
			return new EmptyAgent();

		var log = agentBuilder.Logger;

		log.SetAdditionalLogger(logger);

		var otelBuilder = agentBuilder.Services.AddOpenTelemetry();
		otelBuilder
			.WithTracing(tracing =>
			{
				if (!agentBuilder.SkipOtlpRegistration)
					tracing.AddOtlpExporter(agentBuilder.OtlpExporterName, agentBuilder.OtlpExporterConfiguration);
				log.LogAgentBuilderBuiltTracerProvider();
			})
			.WithMetrics(metrics =>
			{
				if (!agentBuilder.SkipOtlpRegistration)
				{
					metrics.AddOtlpExporter(agentBuilder.OtlpExporterName, o =>
					{
						o.ExportProcessorType = ExportProcessorType.Simple;
						o.Protocol = OtlpExportProtocol.HttpProtobuf;
					});
				}
				log.LogAgentBuilderBuiltMeterProvider();
			});

		var sp = serviceProvider ?? agentBuilder.Services.BuildServiceProvider();
		var tracerProvider = sp.GetService<TracerProvider>()!;
		var meterProvider = sp.GetService<MeterProvider>()!;

		var agent = new ElasticAgent(log, agentBuilder.EventListener, tracerProvider, meterProvider);
		log.LogAgentBuilderBuiltAgent();
		return agent;
	}
}

internal class EmptyAgent : IAgent
{
	public void Dispose() { }

	public ValueTask DisposeAsync() => default;
}

internal class ElasticAgent(
	AgentCompositeLogger logger,
	LoggingEventListener loggingEventListener,
	TracerProvider tracerProvider,
	MeterProvider meterProvider
) : IAgent
{
	public void Dispose()
	{
		tracerProvider.Dispose();
		meterProvider.Dispose();
		loggingEventListener.Dispose();
		logger.Dispose();
	}

	public async ValueTask DisposeAsync()
	{
		tracerProvider.Dispose();
		meterProvider.Dispose();
		await loggingEventListener.DisposeAsync().ConfigureAwait(false);
		await logger.DisposeAsync().ConfigureAwait(false);
	}
}

