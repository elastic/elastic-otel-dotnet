using OpenTelemetry.Metrics;

namespace Elastic.OpenTelemetry.Extensions;

/// <summary> Provides Elastic APM extensions to <see cref="MeterProviderBuilder"/> </summary>
public static class MeterBuilderProviderExtensions
{
    //TODO binder source generator on Build() to make it automatic?
    /// <summary>
    /// TODO
    /// </summary>
    public static MeterProviderBuilder AddElastic(this MeterProviderBuilder builder) =>
		builder
			.AddMeter("Elastic.OpenTelemetry");
}
