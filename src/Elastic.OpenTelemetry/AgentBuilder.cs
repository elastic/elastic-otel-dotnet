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
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Elastic.OpenTelemetry;


/// <summary>
/// Expert options to provide to <see cref="AgentBuilder"/> to control its initial OpenTelemetry registration
/// </summary>
public record AgentBuilderOptions
{
	/// <summary>
	/// Provide an additional logger to the internal file logger.
	/// <para>
	/// The agent will always log to file if a Path is provided using the <c>ELASTIC_OTEL_LOG_DIRECTORY</c>
	/// environment variable.</para>
	/// </summary>
	public ILogger? Logger { get; init; }

	/// <summary>
	/// Provides an <see cref="IServiceCollection"/> to register the agent into.
	/// If null a new local instance will be used.
	/// </summary>
	public IServiceCollection? Services { get; init; }

	/// <summary>
	/// The initial activity sources to listen to.
	/// <para>>These can always later be amended with <see cref="TracerProviderBuilder.AddSource"/></para>
	/// </summary>
	public string[] ActivitySources { get; init; } = [];

	/// <summary>
	/// Stops <see cref="AgentBuilder"/> to register OLTP exporters, useful for testing scenarios
	/// </summary>
	public bool SkipOtlpExporter { get; init; }

	/// <summary>
    /// Optional name which is used when retrieving OTLP options.
	/// </summary>
	public string? OtlpExporterName { get; init; }
}

/// <summary>
/// Supports building <see cref="IAgent"/> instances which include Elastic defaults, but can also be customised.
/// </summary>
public class AgentBuilder : IOpenTelemetryBuilder
{
	internal AgentCompositeLogger Logger { get; }
	internal LoggingEventListener EventListener { get; }

	/// <inheritdoc cref="IOpenTelemetryBuilder.Services"/>
	public IServiceCollection Services { get; }

	/// <summary> TODO </summary>
	public AgentBuilder(params string[] activitySourceNames) : this(new AgentBuilderOptions
	{
		ActivitySources = activitySourceNames
	}) { }

	/// <summary> TODO </summary>
	public AgentBuilder(AgentBuilderOptions options)
	{
		Logger = new AgentCompositeLogger(options.Logger);

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
					metrics.AddOtlpExporter(options.OtlpExporterName, o =>
					{
						o.ExportProcessorType = ExportProcessorType.Simple;
						o.Protocol = OtlpExportProtocol.HttpProtobuf;
					});
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
