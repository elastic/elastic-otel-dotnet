// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Diagnostics;
using Elastic.OpenTelemetry.Diagnostics.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Elastic.OpenTelemetry.Extensions;

/// <summary> TODO </summary>
public static class OpenTelemetryBuilderExtensions
{
	/// <summary> TODO </summary>
	public static IOpenTelemetryBuilder WithLogger(this IOpenTelemetryBuilder builder, ILogger logger)
	{
		if (builder is not ElasticOpenTelemetryBuilder agentBuilder)
			return builder;

		agentBuilder.Logger.SetAdditionalLogger(logger);
		return agentBuilder;
	}

	/// <summary>
	/// Build an instance of <see cref="IInstrumentationLifetime"/>.
	/// </summary>
	/// <returns>A new instance of <see cref="IInstrumentationLifetime"/>.</returns>
	public static IInstrumentationLifetime Build(this IOpenTelemetryBuilder builder, ILogger? logger = null, IServiceProvider? serviceProvider = null)
	{
		// this happens if someone calls Build() while using IServiceCollection and AddOpenTelemetry() and NOT Add*Elastic*OpenTelemetry()
		// we treat this a NOOP
		// NOTE for AddElasticOpenTelemetry(this IServiceCollection services) calling Build() manually is NOT required.
		if (builder is not ElasticOpenTelemetryBuilder agentBuilder)
			return new EmptyInstrumentationLifetime();

		var log = agentBuilder.Logger;

		log.SetAdditionalLogger(logger);

		var sp = serviceProvider ?? agentBuilder.Services.BuildServiceProvider();
		var tracerProvider = sp.GetService<TracerProvider>()!;
		var meterProvider = sp.GetService<MeterProvider>()!;

		var agent = new InstrumentationLifetime(log, agentBuilder.EventListener, tracerProvider, meterProvider);
		log.LogAgentBuilderBuiltAgent();
		return agent;
	}
}

