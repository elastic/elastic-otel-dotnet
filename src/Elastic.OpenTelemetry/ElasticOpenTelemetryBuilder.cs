// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Diagnostics;
using Elastic.OpenTelemetry.Diagnostics.Logging;
using Elastic.OpenTelemetry.Extensions;
using Elastic.OpenTelemetry.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Elastic.OpenTelemetry;

/// <summary>
/// An implementation of <see cref="IOpenTelemetryBuilder" /> which configures Elastic defaults, but can also be customised.
/// </summary>
/// <remarks>
/// Currently this builder enables both tracing and metrics, and configures the following:
/// <list type="bullet">
///   <item>
///     <term>Instrumentation</term>
///     <description>Enables commonly used instrumentation such as HTTP, gRPC and EntityFramework.</description>
///   </item>
///   <item>
///     <term>Processors</term>
///     <description>Enables Elastic processors to add additional features and to
///     ensure data is compatible with Elastic backends.</description>
///   </item>
///   <item>
///     <term>OTLP Exporter</term>
///     <description>Enables exporting of signals over OTLP to a configured endpoint(s).</description>
///   </item>
/// </list>
/// </remarks>
public class ElasticOpenTelemetryBuilder : IOpenTelemetryBuilder
{
	internal CompositeLogger Logger { get; }
	internal LoggingEventListener EventListener { get; }

	/// <inheritdoc cref="IOpenTelemetryBuilder.Services"/>
	public IServiceCollection Services { get; }

	/// <summary>
	/// Creates an instance of the <see cref="ElasticOpenTelemetryBuilder" /> configured with default options.
	/// </summary>
	public ElasticOpenTelemetryBuilder()
		: this(new ElasticOpenTelemetryBuilderOptions())
	{ }

	/// <summary>
	/// Creates an instance of the <see cref="ElasticOpenTelemetryBuilder" /> configured with the provided
	/// <see cref="ElasticOpenTelemetryBuilderOptions"/>.
	/// </summary>
	public ElasticOpenTelemetryBuilder(ElasticOpenTelemetryBuilderOptions options)
	{
		Logger = new CompositeLogger(options);

		// Enables logging of OpenTelemetry-SDK event source events
		EventListener = new LoggingEventListener(Logger, options.DistroOptions);

		Logger.LogAgentPreamble();
		Logger.LogElasticOpenTelemetryBuilderInitialized(Environment.NewLine, new StackTrace(true));
		options.DistroOptions.LogConfigSources(Logger);

		Services = options.Services ?? new ServiceCollection();

		if (options.Services is not null && !options.Services.Any(d => d.ImplementationType == typeof(ElasticOpenTelemetryService)))
			Services.Insert(0, ServiceDescriptor.Singleton<IHostedService, ElasticOpenTelemetryService>());

		Services.TryAddSingleton(this);

		// Directly invoke the SDK extension method to ensure SDK components are registered.
		var openTelemetry =
			Microsoft.Extensions.DependencyInjection.OpenTelemetryServicesExtensions.AddOpenTelemetry(Services);

		// We always add this so we can identify a distro is being used, even if all Elastic defaults are disabled.
		openTelemetry.ConfigureResource(r => r.AddDistroAttributes());

		if (options.DistroOptions.EnabledDefaults.Equals(ElasticOpenTelemetryOptions.EnabledElasticDefaults.None))
			return;

		//https://github.com/open-telemetry/opentelemetry-dotnet/pull/5400
		if (!options.DistroOptions.SkipOtlpExporter)
			openTelemetry.UseOtlpExporter();

		if (options.DistroOptions.EnabledDefaults.HasFlag(ElasticOpenTelemetryOptions.EnabledElasticDefaults.Logging))
		{
			//TODO Move to WithLogging once it gets stable
			Services.Configure<OpenTelemetryLoggerOptions>(logging =>
			{
				logging.IncludeFormattedMessage = true;
				logging.IncludeScopes = true;
				//TODO add processor that adds service.id
			});
		}

		if (options.DistroOptions.EnabledDefaults.HasFlag(ElasticOpenTelemetryOptions.EnabledElasticDefaults.Tracing))
		{
			openTelemetry.WithTracing(tracing =>
			{
				tracing
					.AddHttpClientInstrumentation()
					.AddGrpcClientInstrumentation();

#if !NETFRAMEWORK
				tracing.AddEntityFrameworkCoreInstrumentation();
#endif
				tracing.AddElasticProcessors(Logger);

				Logger.LogConfiguredTracerProvider();
			});
		}

		if (options.DistroOptions.EnabledDefaults.HasFlag(ElasticOpenTelemetryOptions.EnabledElasticDefaults.Metrics))
		{
			openTelemetry.WithMetrics(metrics =>
			{
				metrics
					.AddProcessInstrumentation()
					.AddRuntimeInstrumentation()
					.AddHttpClientInstrumentation();

				Logger.LogConfiguredMeterProvider();
			});
		}
	}
}

internal static partial class LoggerMessages
{
	[LoggerMessage(EventId = 0, Level = LogLevel.Trace, Message = "ElasticOpenTelemetryBuilder initialized{newline}{StackTrace}.")]
	public static partial void LogElasticOpenTelemetryBuilderInitialized(this ILogger logger, string newline, StackTrace stackTrace);

	[LoggerMessage(EventId = 1, Level = LogLevel.Trace, Message = "ElasticOpenTelemetryBuilder configured tracing services via the TracerProvider.")]
	public static partial void LogConfiguredTracerProvider(this ILogger logger);

	[LoggerMessage(EventId = 2, Level = LogLevel.Trace, Message = "ElasticOpenTelemetryBuilder configured metric services via the MeterProvider.")]
	public static partial void LogConfiguredMeterProvider(this ILogger logger);
}
