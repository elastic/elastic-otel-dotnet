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

    // TODO - We need to decide which sources and how to handle conditional things such as ASP.NET Core.
    private readonly TracerProviderBuilder _tracerProviderBuilder =
        Sdk.CreateTracerProviderBuilder()
            .AddHttpClientInstrumentation()
            .AddGrpcClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation();

    private readonly MeterProviderBuilder _meterProvider =
        Sdk.CreateMeterProviderBuilder()
            .AddProcessInstrumentation()
            .AddRuntimeInstrumentation()
            .AddHttpClientInstrumentation();

    private Action<OtlpExporterOptions>? _otlpExporerConfiguration;
    private string? _otlpExporerName;

    public AgentBuilder() => Tracer = new Tracer(this, _tracerProviderBuilder);

    /// <summary>
    /// Build an instance of <see cref="IAgent"/>.
    /// </summary>
    /// <returns>A new instance of <see cref="IAgent"/>.</returns>
    public IAgent Build()
    {
        //// TODO - These always apply after our defaults.
        //// What about cases where users want to register processors before any exporters we add by default (OTLP)?
        //traceConfiguration?.Invoke(_tracerProvider);
        //metricConfiguration?.Invoke(_meterProvider);

        // TODO: In the future we will allow consumers to register additional exporters. We need to consider how adding this exporter
        // may affect those and the order of registration.
        _tracerProviderBuilder.AddElasticExporter(_otlpExporerConfiguration, _otlpExporerName);

        if (Tracer.ResourceBuilderAction is not null)
        {
            var action = Tracer.ResourceBuilderAction;
            action += b => b.AddDistroAttributes();
            _tracerProviderBuilder.ConfigureResource(action);
        }
        else
        {
            _tracerProviderBuilder.ConfigureResource(DefaultResourceBuilderConfiguration);
        }

        var tracerProvider = _tracerProviderBuilder.Build();

        return tracerProvider is not null ? new Agent(tracerProvider) : new Agent();
    }

    public void ConfigureOtlpExporter(Action<OtlpExporterOptions> configure, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _otlpExporerConfiguration = configure;
        _otlpExporerName = name;
    }

    public Tracer Tracer { get; }

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
