using Elastic.OpenTelemetry.Extensions;

using OpenTelemetry;

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Elastic.OpenTelemetry.Processors;

public class SpanCompressionProcessor : BaseProcessor<Activity>
{
    private readonly ConditionalWeakTable<Activity, Activity> _compressionBuffer = [];

    public override void OnStart(Activity data)
    {
        if (data.DisplayName == "ChildSpanCompression")
            data.SetCustomProperty("IsExitSpan", true); // Later, we'll have to infer this from the Activity Source and Name (if practical)

        base.OnStart(data);
    }

    public override void OnEnd(Activity data)
    {
        if (data.Parent is null)
        {
            base.OnEnd(data);
            return;
        }

        var property = data.GetCustomProperty("IsExitSpan");

        if (!IsCompressionEligible(data, property) || data.Parent!.IsStopped)
        {
            FlushBuffer(data.Parent!);
            base.OnEnd(data);
            return;
        }

        if (_compressionBuffer.TryGetValue(data.Parent!, out var compressionBuffer))
        {
            if (!compressionBuffer.TryCompress(data))
            {
                FlushBuffer(data.Parent!);
                _compressionBuffer.Add(data.Parent!, data);
            }
        }
        else
        {
            _compressionBuffer.Add(data.Parent!, data);
            data.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
        }

        base.OnEnd(data);

        static bool IsCompressionEligible(Activity data, object? property) =>
            property is bool isExitSpan && isExitSpan && data.Status is ActivityStatusCode.Ok or ActivityStatusCode.Unset;

        void FlushBuffer(Activity data)
        {
            if (_compressionBuffer.TryGetValue(data, out var compressionBuffer))
            {
                // This recreates the initial activity now we know it's final end time and can record it.
                using var activity = compressionBuffer.Source.StartActivity(compressionBuffer.DisplayName, compressionBuffer.Kind, compressionBuffer.Parent!.Context,
                compressionBuffer.TagObjects, compressionBuffer.Links, compressionBuffer.StartTimeUtc);

                var property = compressionBuffer.GetCustomProperty("Composite");

                if (property is Composite composite)
                {
                    activity?.SetTag("span_compression.strategy", composite.CompressionStrategy);
                    activity?.SetTag("span_compression.count", composite.Count);
                    activity?.SetTag("span_compression.duration", composite.DurationSum);
                }

                var endTime = compressionBuffer.StartTimeUtc.Add(compressionBuffer.Duration);
                activity?.SetEndTime(endTime);
                activity?.Stop();
                _compressionBuffer.Remove(data);
            }
        }
    }
}
