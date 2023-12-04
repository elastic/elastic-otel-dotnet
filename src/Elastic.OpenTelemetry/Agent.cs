using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Elastic.OpenTelemetry;

/// <summary>
/// Supports building and accessing an <see cref="IAgent"/> which collects and ships observability signals.
/// </summary>
public static class Agent
{
    private static readonly object Lock = new();
    private static IAgent? _current;

    /// <summary>
    /// Returns the singleton <see cref="IAgent"/> instance.
    /// </summary>
    /// <remarks>
    /// If an instance is not already initialized, this will create and return a 
    /// default <see cref="IAgent"/> configured with recommended Elastic defaults.
    /// </remarks>
    public static IAgent Current
    {
        get
        {
            if (_current is not null) return _current;

            lock (Lock)
            {
                // disable to satisfy double check lock pattern analyzer
                // ReSharper disable once InvertIf
                if (_current is null)
                {
                    var agent = new AgentBuilder(Resource.Default).Build();
                    _current = agent;
                }
                return _current;
            }
        }
    }

    /// <summary>
    /// Builds an <see cref="IAgent"/>.
    /// </summary>
    /// <param name="traceConfiguration">An action which configures the OpenTelemetry <see cref="TracerProvider"/>.</param>
    /// <param name="metricConfiguration">An action which configures the OpenTelemetry <see cref="MeterProvider"/>.</param>
    /// <returns>An <see cref="IAgent"/> instance.</returns>
    /// <exception cref="Exception">
    /// An exception will be thrown if <see cref="Build(Action{TracerProviderBuilder}?, Action{MeterProviderBuilder}?)"/>
    /// is called more than once during the lifetime of an application.
    /// </exception>
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
            var agentBuilder = new AgentBuilder(Resource.Default);
            var agent = agentBuilder.Build(traceConfiguration, metricConfiguration);
            _current = agent;
            return _current;
        }
    }
}
