using OpenTelemetry;

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Elastic.OpenTelemetry.Processors;

public class SpanCounterProcessor : BaseProcessor<Activity>
{
    private static readonly Meter Meter = new("Elastic.OpenTelemetry", "1.0.0");
    private static readonly Counter<int> Counter = Meter.CreateCounter<int>("span-export-count");

    public override void OnEnd(Activity data)
    {
        Counter.Add(1);
        base.OnEnd(data);
    }
}
