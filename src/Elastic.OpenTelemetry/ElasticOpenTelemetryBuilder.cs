// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using Elastic.OpenTelemetry.Diagnostics;
using Elastic.OpenTelemetry.Diagnostics.Logging;
using Elastic.OpenTelemetry.Extensions;
using Elastic.OpenTelemetry.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Elastic.OpenTelemetry;

/// <summary>
/// Supports building <see cref="IElasticOpenTelemetry"/> instances which include Elastic defaults, but can also be customised.
/// </summary>
public class ElasticOpenTelemetryBuilder : IOpenTelemetryBuilder
{
	internal CompositeLogger Logger { get; }
	internal LoggingEventListener EventListener { get; }

	/// <inheritdoc cref="IOpenTelemetryBuilder.Services"/>
	public IServiceCollection Services { get; }

	/// <summary> TODO </summary>
	public ElasticOpenTelemetryBuilder(params string[] activitySourceNames) : this(new ElasticOpenTelemetryOptions
	{
		ActivitySources = activitySourceNames
	})
	{ }

	/// <summary> TODO </summary>
	public ElasticOpenTelemetryBuilder(ElasticOpenTelemetryOptions options)
	{
		Logger = new CompositeLogger(options.Logger);

		// Enables logging of OpenTelemetry-SDK event source events
		EventListener = new LoggingEventListener(Logger);

		Logger.LogAgentPreamble();
		Logger.LogAgentBuilderInitialized(Environment.NewLine, new StackTrace(true));
		Services = options.Services ?? new ServiceCollection();

		if (options.Services != null)
			Services.AddHostedService<ElasticOtelDistroService>();

		Services.AddSingleton(this);

		var openTelemetry =
			Microsoft.Extensions.DependencyInjection.OpenTelemetryServicesExtensions.AddOpenTelemetry(Services);

		openTelemetry
			.WithTracing(tracing =>
			{
				tracing.ConfigureResource(r => r.AddDistroAttributes());

				foreach (var source in options.ActivitySources)
					tracing.LogAndAddSource(source, Logger);

				tracing
					.AddHttpClientInstrumentation()
					.AddGrpcClientInstrumentation()
					.AddEntityFrameworkCoreInstrumentation(); // TODO - Should we add this by default?

				tracing.AddElasticProcessors(Logger);
			})
			.WithMetrics(metrics =>
			{
				metrics.ConfigureResource(r => r.AddDistroAttributes());

				foreach (var source in options.ActivitySources)
				{
					Logger.LogMeterAdded(source, metrics.GetType().Name);
					metrics.AddMeter(source);
				}

				metrics
					.AddProcessInstrumentation()
					.AddRuntimeInstrumentation()
					.AddHttpClientInstrumentation();
			});

		openTelemetry
			.WithTracing(tracing =>
			{
				if (!options.SkipOtlpExporter)
					tracing.AddOtlpExporter(options.OtlpExporterName, _ => { });
				Logger.LogAgentBuilderBuiltTracerProvider();
			})
			.WithMetrics(metrics =>
			{
				if (!options.SkipOtlpExporter)
				{
					metrics.AddOtlpExporter(options.OtlpExporterName, _ => { });
				}
				Logger.LogAgentBuilderBuiltMeterProvider();
			});

		Logger.LogAgentBuilderRegisteredServices();
	}
}

internal static partial class LoggerMessages
{
	[LoggerMessage(EventId = 0, Level = LogLevel.Trace, Message = $"AgentBuilder initialized{{newline}}{{StackTrace}}.")]
	public static partial void LogAgentBuilderInitialized(this ILogger logger, string newline, StackTrace stackTrace);

	[LoggerMessage(EventId = 0, Level = LogLevel.Trace, Message = "AgentBuilder built TracerProvider.")]
	public static partial void LogAgentBuilderBuiltTracerProvider(this ILogger logger);

	[LoggerMessage(EventId = 0, Level = LogLevel.Trace, Message = "AgentBuilder built MeterProvider.")]
	public static partial void LogAgentBuilderBuiltMeterProvider(this ILogger logger);

	[LoggerMessage(EventId = 0, Level = LogLevel.Trace, Message = "AgentBuilder built Agent.")]
	public static partial void LogAgentBuilderBuiltAgent(this ILogger logger);

	[LoggerMessage(EventId = 0, Level = LogLevel.Trace, Message = "AgentBuilder registered agent services into IServiceCollection.")]
	public static partial void LogAgentBuilderRegisteredServices(this ILogger logger);
}
