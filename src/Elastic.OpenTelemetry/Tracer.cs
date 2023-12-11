using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Elastic.OpenTelemetry;

public class Tracer(AgentBuilder agentBuilder, TracerProviderBuilder tracerProviderBuilder)
{
    // TODO - This is a POC of a mechanism to configure the tracer.
    // Right now the methods return the agent builder, which is nice if you only need to update one thing on the tracer
    // It let us do, var agent = new AgentBuilder().Tracer.AddSource("CustomActivitySource").Build();
    // However, it doesn't work so nicely it you want to update the tracer multiple times as we would need to call Tracer each time
    // e.g. new AgentBuilder().Tracer.AddSource("CustomActivitySource1").Tracer.AddSource("CustomActivitySource2").Build()

    private readonly AgentBuilder _agentBuilder = agentBuilder;
    private readonly TracerProviderBuilder _tracerProviderBuilder = tracerProviderBuilder;

    public AgentBuilder AddSource(string source)
    {
        _tracerProviderBuilder.AddSource(source);
        return _agentBuilder;
    }

    public AgentBuilder ConfigureResourceBuilder(Action<ResourceBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        ResourceBuilderAction = configure;
        return _agentBuilder;
    }

    internal Action<ResourceBuilder>? ResourceBuilderAction { get; private set; }
}
