// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using Elastic.OpenTelemetry.Diagnostics;
using Elastic.OpenTelemetry.Diagnostics.Logging;
using Elastic.OpenTelemetry.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Elastic.OpenTelemetry;

/// <summary>
/// Supports building <see cref="IAgent"/> instances which include Elastic defaults, but can also be customised.
/// </summary>
public class AgentBuilder : IOpenTelemetryBuilder
{
	private bool _skipOtlpRegistration;

	internal Action<OtlpExporterOptions>? OtlpExporterConfiguration { get; private set; }
	internal string? OtlpExporterName { get; private set; }
	internal bool SkipOtlpRegistration => _skipOtlpRegistration;
	internal AgentCompositeLogger Logger { get; }
	internal LoggingEventListener EventListener { get; }

	/// <inheritdoc cref="IOpenTelemetryBuilder.Services"/>
	public IServiceCollection Services { get; }

	/// <summary> TODO </summary>
	public AgentBuilder(params string[] activitySourceNames) : this(null, null, activitySourceNames) { }

	/// <summary> TODO </summary>
	public AgentBuilder(ILogger? logger = null, IServiceCollection? services = null, params string[] activitySourceNames)
	{
		Logger = new AgentCompositeLogger(logger);

		// Enables logging of OpenTelemetry-SDK event source events
		EventListener = new LoggingEventListener(Logger);

		Logger.LogAgentPreamble();
		Logger.LogAgentBuilderInitialized(Environment.NewLine, new StackTrace(true));
		Services = services ?? new ServiceCollection();

		if (services != null)
			Services.AddHostedService<ElasticOtelDistroService>();

		Services
			.AddSingleton(this)
			.AddOpenTelemetry()
			.WithTracing(tracing =>
			{
				tracing.ConfigureResource(r => r.AddDistroAttributes());

				foreach (var source in activitySourceNames)
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

				foreach (var source in activitySourceNames)
				{
					Logger.LogMeterAdded(source, metrics.GetType().Name);
					metrics.AddMeter(source);
				}


				metrics
					.AddProcessInstrumentation()
					.AddRuntimeInstrumentation()
					.AddHttpClientInstrumentation();
			});
		Logger.LogAgentBuilderRegisteredServices();
	}

	/// <summary> TODO </summary>
	public AgentBuilder SkipOtlpExporter()
	{
		_skipOtlpRegistration = true;
		return this;
	}


	/// <summary>
	/// TODO
	/// </summary>
	public void ConfigureOtlpExporter(Action<OtlpExporterOptions> configure, string? name = null)
	{
		ArgumentNullException.ThrowIfNull(configure);
		OtlpExporterConfiguration = configure;
		OtlpExporterName = name;
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
