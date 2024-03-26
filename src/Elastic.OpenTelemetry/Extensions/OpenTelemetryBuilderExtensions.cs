// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Elastic.OpenTelemetry.Extensions;

/// <summary>
/// Extension methods on <see cref="IOpenTelemetryBuilder"/>.
/// </summary>
public static class OpenTelemetryBuilderExtensions
{
	/// <summary>
	/// Add an <see cref="ILogger" /> to the <see cref="IOpenTelemetryBuilder"/> to which
	/// logs will be written in addition to the configured diagnostic log file.
	/// </summary>
	public static IOpenTelemetryBuilder WithLogger(this IOpenTelemetryBuilder builder, ILogger logger)
	{
		if (builder is not ElasticOpenTelemetryBuilder distributionBuilder)
			return builder;

		distributionBuilder.Logger.SetAdditionalLogger(logger);
		return distributionBuilder;
	}

	/// <summary>
	/// Triggers creation and registration of the OpenTelemetry components required to begin observing the application.
	/// </summary>
	/// <returns>A new instance of <see cref="IInstrumentationLifetime"/> which supports disposing of the
	/// OpenTelemetry providers to end signal collection.</returns>
	public static IInstrumentationLifetime Build(this IOpenTelemetryBuilder builder, ILogger? logger = null, IServiceProvider? serviceProvider = null)
	{
		// this happens if someone calls Build() while using IServiceCollection and AddOpenTelemetry() and NOT Add*Elastic*OpenTelemetry()
		// we treat this a NOOP
		// NOTE for AddElasticOpenTelemetry(this IServiceCollection services) calling Build() manually is NOT required.
		if (builder is not ElasticOpenTelemetryBuilder elasticOtelBuilder)
			return new EmptyInstrumentationLifetime();

		var compositeLogger = elasticOtelBuilder.Logger;
		compositeLogger.SetAdditionalLogger(logger);

		var sp = serviceProvider ?? elasticOtelBuilder.Services.BuildServiceProvider();
		var tracerProvider = sp.GetService<TracerProvider>()!;
		var meterProvider = sp.GetService<MeterProvider>()!;

		var lifetime = new InstrumentationLifetime(compositeLogger, elasticOtelBuilder.EventListener, tracerProvider, meterProvider);
		compositeLogger.LogElasticOpenTelemetryBuilderBuilt();
		return lifetime;
	}
}

internal static partial class LoggerMessages
{
	[LoggerMessage(EventId = 10, Level = LogLevel.Trace, Message = "ElasticOpenTelemetryBuilder built.")]
	public static partial void LogElasticOpenTelemetryBuilderBuilt(this ILogger logger);
}
