// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using System.Diagnostics.Tracing;
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
using static Elastic.OpenTelemetry.Configuration.ElasticOpenTelemetryOptions;

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
	/// Shared bootstrap routine for the Elastic Distribution for OpenTelemetry .NET.
	/// Used to ensure auto instrumentation and manual instrumentation bootstrap the same way.
	/// </summary>
	public static (EventListener, ILogger) Bootstrap(ElasticOpenTelemetryBuilderOptions options)
	{
		var logger = new CompositeLogger(options);

		// Enables logging of OpenTelemetry-SDK event source events
		var eventListener = new LoggingEventListener(logger, options.DistroOptions);

		logger.LogAgentPreamble();
		logger.LogElasticOpenTelemetryBuilderInitialized(Environment.NewLine, new StackTrace(true));
		options.DistroOptions.LogConfigSources(logger);
		return (eventListener, logger);
	}

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
		var (eventListener, logger) = Bootstrap(options);

		Logger = (CompositeLogger)logger;
		EventListener = (LoggingEventListener)eventListener;

		Services = options.Services ?? new ServiceCollection();

		if (options.Services is not null && !options.Services.Any(d => d.ImplementationType == typeof(ElasticOpenTelemetryService)))
			Services.Insert(0, ServiceDescriptor.Singleton<IHostedService, ElasticOpenTelemetryService>());

		Services.TryAddSingleton(this);

		// Directly invoke the SDK extension method to ensure SDK components are registered.
		var openTelemetry =
			Microsoft.Extensions.DependencyInjection.OpenTelemetryServicesExtensions.AddOpenTelemetry(Services);

		// We always add this so we can identify a distro is being used, even if all Elastic defaults are disabled.
		openTelemetry.ConfigureResource(r => r.UseElasticDefaults());

		if (options.DistroOptions.Defaults.Equals(ElasticDefaults.None))
		{
			Logger.LogNoElasticDefaults();

			// We always add the distro attribute so that we can identify a distro is being used, even if all Elastic defaults are disabled.
			openTelemetry.ConfigureResource(r => r.AddDistroAttributes());
			return;
		}

		openTelemetry.ConfigureResource(r => r.UseElasticDefaults(Logger));

		//https://github.com/open-telemetry/opentelemetry-dotnet/pull/5400
		if (!options.DistroOptions.SkipOtlpExporter)
			openTelemetry.UseOtlpExporter();

		if (options.DistroOptions.Defaults.HasFlag(ElasticDefaults.Logging))
		{
			//TODO Move to WithLogging once it gets stable
			Services.Configure<OpenTelemetryLoggerOptions>(logging => logging.UseElasticDefaults());
		}

		if (options.DistroOptions.Defaults.HasFlag(ElasticDefaults.Tracing))
			openTelemetry.WithTracing(tracing => tracing.UseElasticDefaults(Logger));

		if (options.DistroOptions.Defaults.HasFlag(ElasticDefaults.Metrics))
			openTelemetry.WithMetrics(metrics => metrics.UseElasticDefaults(Logger));
	}
}

internal static partial class LoggerMessages
{
	[LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "ElasticOpenTelemetryBuilder initialized{newline}{StackTrace}.")]
	public static partial void LogElasticOpenTelemetryBuilderInitialized(this ILogger logger, string newline, StackTrace stackTrace);

	[LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "ElasticOpenTelemetryBuilder configured {Signal} via the {Provider}.")]
	public static partial void LogConfiguredSignalProvider(this ILogger logger, string signal, string provider);

	[LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "No Elastic defaults were enabled.")]
	public static partial void LogNoElasticDefaults(this ILogger logger);
}
