using Elastic.OpenTelemetry.Extensions;
using Elastic.OpenTelemetry.Resources;
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
    private static readonly Action<ResourceBuilder> DefaultResourceBuilderConfiguration = builder =>
        builder
            .Clear()
            .AddDetector(new DefaultServiceDetector())
            .AddTelemetrySdk()
            .AddDetector(new ElasticEnvironmentVariableDetector())
            .AddEnvironmentVariableDetector()
            .AddDistroAttributes();

    private readonly TracerProviderBuilder _tracerProviderBuilder = Sdk.CreateTracerProviderBuilder();

    private readonly MeterProviderBuilder _meterProvider =
        Sdk.CreateMeterProviderBuilder()
            .AddProcessInstrumentation()
            .AddRuntimeInstrumentation()
            .AddHttpClientInstrumentation();

    private readonly string[]? _activitySourceNames;
    private Action<TracerProviderBuilder>? _tracerProviderBuilderAction;
    private Action<ResourceBuilder>? _resourceBuilderAction;
    private Action<OtlpExporterOptions>? _otlpExporerConfiguration;
    private string? _otlpExporerName;

    /// <summary>
    /// TODO
    /// </summary>
    public AgentBuilder() { }

    // NOTE - Applies to all signals
    /// <summary>
    /// TODO
    /// </summary>
    public AgentBuilder(params string[] activitySourceNames) => _activitySourceNames = activitySourceNames;

    // NOTE: The builder methods below are extremely experimental and will go through a final API design and
    // refinement before alpha 1

    /// <summary>
    /// TODO
    /// </summary>
    public AgentBuilder AddTracerSource(string activitySourceName)
    {
        ArgumentException.ThrowIfNullOrEmpty(activitySourceName);
        _tracerProviderBuilder.AddSource(activitySourceName);
        return this;
    }

    /// <summary>
    /// TODO
    /// </summary>
    public AgentBuilder AddTracerSources(string activitySourceNameA, string activitySourceNameB)
    {
        ArgumentException.ThrowIfNullOrEmpty(activitySourceNameA);
        ArgumentException.ThrowIfNullOrEmpty(activitySourceNameB);

        _tracerProviderBuilder.AddSource(activitySourceNameA);
        _tracerProviderBuilder.AddSource(activitySourceNameB);

        return this;
    }

    // TODO - Other AddTracerSources for up to x sources to avoid params allocation.

    /// <summary>
    /// TODO
    /// </summary>
    public AgentBuilder AddTracerSources(params string[] activitySourceNames)
    {
        _tracerProviderBuilder.AddSource(activitySourceNames);
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
    public AgentBuilder ConfigureTracer(Action<ResourceBuilder> configureResourceBuilder)
    {
        TracerInternal(configureResourceBuilder, null);
        return this;
    }

    /// <summary>
    /// TODO
    /// </summary>
    public AgentBuilder ConfigureTracer(Action<ResourceBuilder> configureResourceBuilder, params string[] activitySourceNames)
    {
        TracerInternal(configureResourceBuilder, activitySourceNames);
        return this;
    }

    /// <summary>
    /// TODO
    /// </summary>
    public AgentBuilder ConfigureTracer(Action<ResourceBuilder> configureResourceBuilder, string activitySourceName)
    {
        TracerInternal(configureResourceBuilder, [activitySourceName]);
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
        _tracerProviderBuilderAction = configure;
        return this;
    }

    private AgentBuilder TracerInternal(Action<ResourceBuilder>? configureResourceBuilder = null, string[]? activitySourceNames = null)
    {
        _resourceBuilderAction = configureResourceBuilder;

        if (activitySourceNames is not null)
            _tracerProviderBuilder.AddSource(activitySourceNames);

        return this;
    }

    /// <summary>
    /// Build an instance of <see cref="IAgent"/>.
    /// </summary>
    /// <returns>A new instance of <see cref="IAgent"/>.</returns>
    public IAgent Build()
    {
        if (_tracerProviderBuilderAction is null)
        {
            if (_activitySourceNames is not null)
                _tracerProviderBuilder.AddSource(_activitySourceNames);

            // Set up a default tracer provider.
            // TODO - We need to decide which sources and how to handle conditional things such as ASP.NET Core.
            _tracerProviderBuilder
               .AddHttpClientInstrumentation()
               .AddGrpcClientInstrumentation()
               .AddEntityFrameworkCoreInstrumentation()
               .AddElasticProcessors();

            if (_resourceBuilderAction is not null)
            {
                var action = _resourceBuilderAction;
                action += b => b.AddDistroAttributes();
                _tracerProviderBuilder.ConfigureResource(action);
            }
            else
            {
                _tracerProviderBuilder.ConfigureResource(DefaultResourceBuilderConfiguration);
            }
        }
        else
        {
            // Add Elastic processors before consumer action is invoked, so that they run before everything else.
            // TODO - What about cases where users want to register processors which run before ours?
            _tracerProviderBuilder.AddElasticProcessors();

            // Then apply the consumer configuration.
            _tracerProviderBuilderAction.Invoke(_tracerProviderBuilder);
        }

        // Ensure the distro attributes are always added to the resource.
        _tracerProviderBuilder.ConfigureResource(r => r.AddDistroAttributes());

        // Add the OTLP exporter configured to ship data to an Elastic backend.
        // TODO - What about cases where users want to register processors/exporters after any exporters we add by default (OTLP)?
        _tracerProviderBuilder.AddElasticOtlpExporter(_otlpExporerConfiguration, _otlpExporerName);

        var tracerProvider = _tracerProviderBuilder.Build();

        return tracerProvider is not null ? new Agent(tracerProvider) : new Agent();
    }

    /// <summary>
    /// TODO
    /// </summary>
    public void ConfigureOtlpExporter(Action<OtlpExporterOptions> configure, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _otlpExporerConfiguration = configure;
        _otlpExporerName = name;
    }

    private sealed class Agent(TracerProvider? tracerProvider, MeterProvider? meterProvider) : IAgent
    {
        private bool _disposedValue;

        private readonly TracerProvider? _tracerProvider = tracerProvider;
        private readonly MeterProvider? _meterProvider = meterProvider;

        internal Agent() : this(null, null)
        {
        }

        internal Agent(TracerProvider tracerProvider) : this(tracerProvider, null)
        {
        }

        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _tracerProvider?.Dispose();
                    _meterProvider?.Dispose();
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
