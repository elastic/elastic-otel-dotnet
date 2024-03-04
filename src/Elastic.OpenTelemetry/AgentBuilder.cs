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
public class AgentBuilder
{
	private readonly MeterProviderBuilder _meterProvider =
		Sdk.CreateMeterProviderBuilder()
			.AddProcessInstrumentation()
			.AddRuntimeInstrumentation()
			.AddHttpClientInstrumentation();

	private readonly List<string> _activitySourceNames = [];
	private Action<TracerProviderBuilder> _tracerProviderBuilderAction = tpb => { };
	private Action<ResourceBuilder>? _resourceBuilderAction = rb => { };
	private Action<OtlpExporterOptions>? _otlpExporterConfiguration;
	private string? _otlpExporterName;

	private readonly AgentCompositeLogger _logger;
	private bool _skipOtlpRegistration;
	private readonly LoggingEventListener _loggingEventListener;

	/// <summary>
	/// TODO
	/// </summary>
	public AgentBuilder(ILogger? logger = null)
	{
		_logger = new AgentCompositeLogger(logger);

		// Enables logging of OpenTelemetry-SDK event source events
		_loggingEventListener = new LoggingEventListener(_logger);

		_logger.LogAgentPreamble();
		_logger.LogAgentBuilderInitialized(Environment.NewLine, new StackTrace(true));
	}


	// NOTE - Applies to all signals
	/// <summary>
	/// TODO
	/// </summary>
	public AgentBuilder(params string[] activitySourceNames) : this() => _activitySourceNames = activitySourceNames.ToList();

	// NOTE: The builder methods below are extremely experimental and will go through a final API design and
	// refinement before alpha 1

	/// <summary>
	/// TODO
	/// </summary>
	public AgentBuilder AddActivitySources(params string[] sources)
	{
		_activitySourceNames.AddRange(sources);
		return this;
	}

	/// <summary>
	/// TODO
	/// </summary>
	public AgentBuilder AddTracerSource(string activitySourceName)
	{
		ArgumentException.ThrowIfNullOrEmpty(activitySourceName);
		_tracerProviderBuilderAction += tpb => tpb.AddSource(activitySourceName);
		return this;
	}

	/// <summary>
	/// TODO
	/// </summary>
	public AgentBuilder AddTracerSources(string activitySourceNameA, string activitySourceNameB)
	{
		ArgumentException.ThrowIfNullOrEmpty(activitySourceNameA);
		ArgumentException.ThrowIfNullOrEmpty(activitySourceNameB);

		_tracerProviderBuilderAction += tpb => tpb.AddSource(activitySourceNameA);
		_tracerProviderBuilderAction += tpb => tpb.AddSource(activitySourceNameB);

		return this;
	}

	// TODO - Other AddTracerSources for up to x sources to avoid params allocation.

	/// <summary>
	/// TODO
	/// </summary>
	public AgentBuilder AddTracerSources(params string[] activitySourceNames)
	{
		_tracerProviderBuilderAction += tpb => tpb.AddSource(activitySourceNames);
		return this;
	}

	/// <summary>
	/// TODO
	/// </summary>
	public AgentBuilder ConfigureResource(Action<ResourceBuilder> configureResourceBuilder)
	{
		// NOTE: Applies to all signals

		ArgumentNullException.ThrowIfNull(configureResourceBuilder);
		_resourceBuilderAction = configureResourceBuilder;
		return this;
	}

	/// <summary>
	/// TODO
	/// </summary>
	public AgentBuilder ConfigureTracer(params string[] activitySourceNames)
	{
		TracerInternal(null, activitySourceNames);
		return this;
	}

	/// <summary>
	/// TODO
	/// </summary>
	public AgentBuilder ConfigureTracer(Action<TracerProviderBuilder> configure)
	{
		// This is the most customisable overload as the consumer can provide a complete
		// Action to configure the TracerProviderBuilder. It is the best option (right now)
		// if a consumer needs to add other instrumentation via extension methods on the
		// TracerProviderBuilder. We will then add the Elastic distro defaults as appropriately
		// as possible. Elastic processors will be registered before running this action. The
		// Elastic exporter will be added after.

		ArgumentNullException.ThrowIfNull(configure);
		_tracerProviderBuilderAction += configure;
		return this;
	}

	private AgentBuilder TracerInternal(Action<ResourceBuilder>? configureResourceBuilder = null, string[]? activitySourceNames = null)
	{
		_resourceBuilderAction = configureResourceBuilder;

		if (activitySourceNames is not null)
			_tracerProviderBuilderAction += tpb => tpb.AddSource(activitySourceNames);

		return this;
	}

	/// <summary>
	/// Build an instance of <see cref="IAgent"/>.
	/// </summary>
	/// <returns>A new instance of <see cref="IAgent"/>.</returns>
	public IAgent Build(ILogger? logger = null)
	{
		_logger.SetAdditionalLogger(logger);
		var tracerProviderBuilder = Sdk.CreateTracerProviderBuilder();
		TracerProviderBuilderAction.Invoke(tracerProviderBuilder);
		var tracerProvider = tracerProviderBuilder.Build();

		_logger.LogAgentBuilderBuiltTracerProvider();

		var agent = new Agent(_logger, _loggingEventListener, tracerProvider);

		_logger.LogAgentBuilderBuiltAgent();

		return agent;
	}

	/// <summary>
	/// Register the OpenTelemetry SDK services and Elastic defaults into the supplied <see cref="IServiceCollection"/>.
	/// </summary>
	/// <param name="serviceCollection">A <see cref="IServiceCollection"/> to which OpenTelemetry SDK services will be added.</param>
	/// <returns>The supplied <see cref="IServiceCollection"/>.</returns>
	public IServiceCollection Register(IServiceCollection serviceCollection)
	{
		// TODO - Docs, we should explain that prior to .NET 8, this needs to be added first if other hosted services emit signals.
		// On .NET 8 we handle this with IHostedLifecycleService

		_ = serviceCollection
			.AddHostedService<ElasticOtelDistroService>()
			.AddOpenTelemetry()
				.WithTracing(TracerProviderBuilderAction);

		_logger.LogAgentBuilderRegisteredServices();

		return serviceCollection;
	}

	/// <summary> TODO </summary>
	public AgentBuilder SkipOtlpExporter()
	{
		_skipOtlpRegistration = true;
		return this;
	}


	private Action<TracerProviderBuilder> TracerProviderBuilderAction =>
		tracerProviderBuilder =>
		{
			foreach (var source in _activitySourceNames)
				tracerProviderBuilder.LogAndAddSource(source, _logger);

			tracerProviderBuilder
				.AddHttpClientInstrumentation()
				.AddGrpcClientInstrumentation()
				.AddEntityFrameworkCoreInstrumentation(); // TODO - Should we add this by default?

			tracerProviderBuilder.AddElasticProcessors(_logger);

			var action = _resourceBuilderAction;
			action += b => b.AddDistroAttributes();
			tracerProviderBuilder.ConfigureResource(action);

			_tracerProviderBuilderAction?.Invoke(tracerProviderBuilder);

			if (!_skipOtlpRegistration)
				tracerProviderBuilder.AddOtlpExporter(_otlpExporterName, _otlpExporterConfiguration);
		};

	/// <summary>
	/// TODO
	/// </summary>
	public void ConfigureOtlpExporter(Action<OtlpExporterOptions> configure, string? name = null)
	{
		ArgumentNullException.ThrowIfNull(configure);
		_otlpExporterConfiguration = configure;
		_otlpExporterName = name;
	}

	private class Agent(
		AgentCompositeLogger logger,
		LoggingEventListener loggingEventListener,
		TracerProvider? tracerProvider = null,
		MeterProvider? meterProvider = null
	) : IAgent
	{
		public void Dispose()
		{
			tracerProvider?.Dispose();
			meterProvider?.Dispose();
			loggingEventListener.Dispose();
			logger.Dispose();
		}

		public async ValueTask DisposeAsync()
		{
			tracerProvider?.Dispose();
			meterProvider?.Dispose();
			await loggingEventListener.DisposeAsync().ConfigureAwait(false);
			await logger.DisposeAsync().ConfigureAwait(false);
		}
	}
}

internal static partial class LoggerMessages
{
	[LoggerMessage(EventId = 0, Level = LogLevel.Trace, Message = $"AgentBuilder initialized{{newline}}{{StackTrace}}.")]
	public static partial void LogAgentBuilderInitialized(this ILogger logger, string newline, StackTrace stackTrace);

	[LoggerMessage(EventId = 0, Level = LogLevel.Trace, Message = "AgentBuilder built TracerProvider.")]
	public static partial void LogAgentBuilderBuiltTracerProvider(this ILogger logger);

	[LoggerMessage(EventId = 0, Level = LogLevel.Trace, Message = "AgentBuilder built Agent.")]
	public static partial void LogAgentBuilderBuiltAgent(this ILogger logger);

	[LoggerMessage(EventId = 0, Level = LogLevel.Trace, Message = "AgentBuilder registered agent services into IServiceCollection.")]
	public static partial void LogAgentBuilderRegisteredServices(this ILogger logger);
}
