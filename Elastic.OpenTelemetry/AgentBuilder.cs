using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Elastic.OpenTelemetry;

public class Service
{
    public Service(string name, string version)
    {
        Name = name;
        Version = version;
    }

    public string Version { get; }

    public string Name { get; }


    private static Service? _calculatedService;
    public static Service DefaultService
    {
        get
        {
            if (_calculatedService != null) return _calculatedService;
            
            // hardcoded for now
            var name = "Example.Elastic.OpenTelemetry";
            var version = "1.0.0";
            _calculatedService = new Service(name, version);
            return _calculatedService;

        }
    }
}
public interface IAgent : IDisposable
{
    Service Service { get; }
}

public static class Agent
{
    public static IAgent Build(
        Action<TracerProviderBuilder>? traceConfiguration = null,
        Action<MeterProviderBuilder>? metricConfiguration = null
    )
    {
        var agentBuilder = new AgentBuilder(Service.DefaultService);
        return agentBuilder.Build(traceConfiguration, metricConfiguration);
    } 
}

public class AgentBuilder 
{
    private readonly TracerProviderBuilder _tracerProvider;
    private readonly MeterProviderBuilder _meterProvider;

    public AgentBuilder(Service service)
    {
        Service = service;
        
        _tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(service.Name)
            .ConfigureResource(resource =>
                resource.AddService(
                    serviceName: service.Name,
                    serviceVersion: service.Version))
            .AddElastic();
        _meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureResource(resource =>
                resource.AddService(
                    serviceName: service.Name,
                    serviceVersion: service.Version))
            .AddElastic();
    }

    public Service Service { get; }

    public IAgent Build(
        Action<TracerProviderBuilder>? traceConfiguration = null,
        Action<MeterProviderBuilder>? metricConfiguration = null
    )
    {
        traceConfiguration?.Invoke(_tracerProvider);
        metricConfiguration?.Invoke(_meterProvider);

        return new Agent(Service, _tracerProvider.Build(), _meterProvider.Build());
    }

    private class Agent : IAgent
    {
        private readonly TracerProvider? _tracerProvider;
        private readonly MeterProvider? _meterProvider;

        public Agent(Service service, TracerProvider? tracerProvider, MeterProvider? meterProvider)
        {
            Service = service;
            _tracerProvider = tracerProvider;
            _meterProvider = meterProvider;
        }
        
        public Service Service { get; }

        public void Dispose()
        {
            //_tracerProvider?.ForceFlush((int)TimeSpan.FromSeconds(5).TotalMilliseconds);
            //_meterProvider?.ForceFlush((int)TimeSpan.FromSeconds(5).TotalMilliseconds);

            _tracerProvider?.Dispose();
            _meterProvider?.Dispose();
        }

    }
}