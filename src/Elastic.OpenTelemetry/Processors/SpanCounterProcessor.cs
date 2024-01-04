using OpenTelemetry;

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Elastic.OpenTelemetry.Processors;

/// <summary> An example processor that emits the number of spans as a metric </summary>
public class SpanCounterProcessor : BaseProcessor<Activity>
{
    private static readonly Meter Meter = new("Elastic.OpenTelemetry", "1.0.0");
    private static readonly Counter<int> Counter = Meter.CreateCounter<int>("span-export-count");

	/// <inheritdoc cref="OnEnd"/>
    public override void OnEnd(Activity data)
    {
        Counter.Add(1);
        base.OnEnd(data);
    }
}
