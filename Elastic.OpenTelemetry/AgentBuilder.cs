using System.ComponentModel.Design;
using System.Net.Http.Headers;
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


    private static Service? CalculatedService = null;
    public static Service DefaultService
    {
        get
        {
            if (CalculatedService != null) return CalculatedService;
            
            // hardcoded for now
            var name = "Example.Elastic.OpenTelemetry";
            var version = "1.0.0";
            CalculatedService = new Service(name, version);
            return CalculatedService;

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
    private readonly TracerProviderBuilder tracerProvider;
    private readonly MeterProviderBuilder meterProvider;

    public AgentBuilder(Service service)
    {
        Service = service;
        
        tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(service.Name)
            .ConfigureResource(resource =>
                resource.AddService(
                    serviceName: service.Name,
                    serviceVersion: service.Version))
            .AddElastic();
        meterProvider = Sdk.CreateMeterProviderBuilder()
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
        traceConfiguration?.Invoke(tracerProvider);
        metricConfiguration?.Invoke(meterProvider);

        return new Agent(Service, tracerProvider.Build(), meterProvider.Build());
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