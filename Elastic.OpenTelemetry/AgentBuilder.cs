using System.Diagnostics;
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
    ActivitySource ActivitySource { get; }
}

public static class Agent
{
    private static readonly object Lock = new();
    private static IAgent? _current;
    public static IAgent Current
    {
        get
        {
            if (_current != null) return _current;
            lock (Lock)
            {
                // disable to satisfy double check lock pattern analyzer
                // ReSharper disable once InvertIf
                if (_current == null)
                {
                    var agent = new AgentBuilder(Service.DefaultService).Build();
                    _current = agent;
                }
                return _current;
            }
        }
    }

    public static IAgent Build(
        Action<TracerProviderBuilder>? traceConfiguration = null,
        Action<MeterProviderBuilder>? metricConfiguration = null
    )
    {
        if (_current != null) 
            throw new Exception($"{nameof(Agent)}.{nameof(Build)} called twice or after {nameof(Agent)}.{nameof(Current)} was accessed.");
        lock (Lock)
        {
            if (_current != null) 
                throw new Exception($"{nameof(Agent)}.{nameof(Build)} called twice or after {nameof(Agent)}.{nameof(Current)} was accessed.");
            var agentBuilder = new AgentBuilder(Service.DefaultService);
            var agent = agentBuilder.Build(traceConfiguration, metricConfiguration);
            _current = agent;
            return _current;
        }
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
            ActivitySource = new ActivitySource(Service.Name);
            _tracerProvider = tracerProvider;
            _meterProvider = meterProvider;
        }
        
        public Service Service { get; }
        public ActivitySource ActivitySource { get; }

        public void Dispose()
        {
            _tracerProvider?.Dispose();
            _meterProvider?.Dispose();
        }
    }
}