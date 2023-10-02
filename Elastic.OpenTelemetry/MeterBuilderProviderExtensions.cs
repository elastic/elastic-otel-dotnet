// ReSharper disable once CheckNamespace

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace OpenTelemetry.Metrics;

public static class MeterBuilderProviderExtensions
{
    //TODO binder source generator on Build() to make it automatic?
    public static MeterProviderBuilder AddElastic(this MeterProviderBuilder builder)
    {
        return builder
            .AddMeter("Elastic.OpenTelemetry");
    }
}