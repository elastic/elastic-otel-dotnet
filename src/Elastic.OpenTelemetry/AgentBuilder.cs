using System.Diagnostics;
using Elastic.OpenTelemetry.Extensions;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Elastic.OpenTelemetry;

/// <summary>
/// Supports building <see cref="IAgent"/> instances which include Elastic defaults, but can also be configured using OpenTelemetry
/// builders.
/// </summary>
/// <param name="resource">A <see cref="OpenTelemetry.Resource"/> instance.</param>
public class AgentBuilder(Resource resource)
{
    private readonly TracerProviderBuilder _tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(resource.ServiceName)
            .ConfigureResource(resourceBuilder =>
                resourceBuilder.AddService(
                    serviceName: resource.ServiceName,
                    serviceVersion: resource.Version))
            .AddElastic();

    private readonly MeterProviderBuilder _meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureResource(resourceBuilder =>
                resourceBuilder.AddService(
                    serviceName: resource.ServiceName,
                    serviceVersion: resource.Version))
            .AddElastic();

    public Resource Resource { get; } = resource;

    public IAgent Build(
        Action<TracerProviderBuilder>? traceConfiguration = null,
        Action<MeterProviderBuilder>? metricConfiguration = null
    )
    {
        // TODO - These always apply after our defaults.
        // What about cases where users want to register processors before any exporters we add by default (OTLP)?
        traceConfiguration?.Invoke(_tracerProvider);
        metricConfiguration?.Invoke(_meterProvider);

        return new Agent(Resource, _tracerProvider.Build(), _meterProvider.Build());
    }

    private class Agent : IAgent
    {
        private readonly TracerProvider? _tracerProvider;
        private readonly MeterProvider? _meterProvider;

        public Agent(Resource resource, TracerProvider? tracerProvider, MeterProvider? meterProvider)
        {
            _tracerProvider = tracerProvider;
            _meterProvider = meterProvider;

            Resource = resource;
            ActivitySource = new ActivitySource(Resource.ServiceName, Resource.Version);
        }

        public Resource Resource { get; }
        public ActivitySource ActivitySource { get; }

        public void Dispose()
        {
            _tracerProvider?.Dispose();
            _meterProvider?.Dispose();
            ActivitySource.Dispose();
        }
    }
}
