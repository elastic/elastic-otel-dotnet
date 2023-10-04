// ReSharper disable once CheckNamespace

using Microsoft.Extensions.Configuration;
using System;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;

// ReSharper disable once CheckNamespace
namespace OpenTelemetry.Trace;

public static class TraceBuilderProviderExtensions
{
    //TODO binder source generator on Build() to make it automatic?
    public static TracerProviderBuilder AddElastic(this TracerProviderBuilder builder)
    {
        return builder
            .AddProcessor(new TransactionIdProcessor())
            .AddProcessor(new StackTraceProcessor())
            .AddProcessor(new SpanCounterProcessor())
            .AddProcessor(new SpanCompressionProcessor())
            .AddProcessor(new BatchActivityExportProcessor(new MyExporter()));
    }
}

public class TransactionIdProcessor : BaseProcessor<Activity>
{
	private readonly AsyncLocal<ActivitySpanId?> _currentTransactionId = new();
    public override void OnStart(Activity activity)
    {
        if (activity.Parent == null)
            _currentTransactionId.Value = activity.SpanId;
        activity.SetTag("transaction.id", _currentTransactionId.Value);
    }
}

public class SpanCounterProcessor : BaseProcessor<Activity>
{
    private static readonly Meter Meter = new("Elastic.OpenTelemetry", "1.0.0");
    private static readonly Counter<int> Counter =  Meter.CreateCounter<int>("span-export-count");

    public override void OnEnd(Activity data)
    {
        Counter.Add(1);
        base.OnEnd(data);
    }
}

public class StackTraceProcessor : BaseProcessor<Activity>
{
    public override void OnStart(Activity data)
    {
        //for now always capture stacktrace on start

        var stacktrace = new StackTrace(true);
        data.SetTag("_stack_trace", stacktrace);
        base.OnStart(data);
    }

    public override void OnEnd(Activity data)
    {
        //for now always capture stacktrace on start
        var stacktrace = data.GetTagItem("_stack_trace") as StackTrace;
        data.SetTag("_stack_trace", null);
        if (stacktrace == null) return;
        if (data.Duration < TimeSpan.FromMilliseconds(2)) return;

        data.SetTag("stacktrace", stacktrace);
        base.OnEnd(data);
    }
}

public class SpanCompressionProcessor : BaseProcessor<Activity>
{
    private readonly ConditionalWeakTable<Activity, Activity> _compressionBuffer = new();

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

        if (!IsCompressionEligable(data, property) || data.Parent!.IsStopped)
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

        static bool IsCompressionEligable(Activity data, object? property) => 
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

internal class MyExporter : BaseExporter<Activity>
{
    public override ExportResult Export(in Batch<Activity> batch)
    {
        using var scope = SuppressInstrumentationScope.Begin();
        Console.WriteLine($"Exporting: {batch.Count:N0} items");
        return ExportResult.Success;
    }
}

internal static class ActivityExtensions
{
    public static bool TryCompress(this Activity buffered, Activity sibling)
    {
        Composite? composite = null;

        var property = buffered.GetCustomProperty("Composite");

        if (property is Composite c)
        {
            composite = c;
        }

        var isAlreadyComposite = composite is not null;

        var canBeCompressed = isAlreadyComposite 
            ? buffered.TryToCompressComposite(sibling, composite!)
            : buffered.TryToCompressRegular(sibling, ref composite);

        if (!canBeCompressed)
            return false;

        if (!isAlreadyComposite)
        {
            composite ??= new Composite();
            composite.Count = 1;
            composite.DurationSum = buffered.Duration.Milliseconds;
        }

        composite!.Count++;
        composite.DurationSum += sibling.Duration.Milliseconds;

        buffered.SetCustomProperty("Composite", composite);

        var endTime = sibling.StartTimeUtc.Add(sibling.Duration);
        buffered.SetEndTime(endTime);

        sibling.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;

        return true;
    }

    private static bool TryToCompressRegular(this Activity buffered, Activity sibling, ref Composite? composite)
    {
        if (!buffered.IsSameKind(sibling))
            return false;

        if (buffered.OperationName == sibling.OperationName)
        {
            // TODO - Duration configuration check

            composite ??= new Composite();
            composite.CompressionStrategy = "exact_match";
            return true;
        }

        // TODO - Duration configuration check
        composite ??= new Composite();
        composite.CompressionStrategy = "same_kind";
        // TODO - Set name
        return true;
    }

    private static bool TryToCompressComposite(this Activity buffered, Activity sibling, Composite composite)
    {
        switch (composite.CompressionStrategy)
        {
            case "exact_match":
                return buffered.IsSameKind(sibling) && buffered.OperationName == sibling.OperationName; // && sibling.Duration <= Configuration.SpanCompressionExactMatchMaxDuration;

            case "same_kind":
                return buffered.IsSameKind(sibling); // && sibling.Duration <= Configuration.SpanCompressionSameKindMaxDuration;
        }

        return false;
    }

    // TODO - Further implementation if possible
    private static bool IsSameKind(this Activity current, Activity other) =>
        current.Kind == other.Kind;
        // We don't have a direct way to establish which attribute(s) to use to assess these
        //&& Subtype == other.Subtype
        //&& _context.IsValueCreated && other._context.IsValueCreated
        //&& Context?.Service?.Target == other.Context?.Service?.Target;
}

// TODO - Consider a struct but consider if this would get copied too much
internal class Composite
{
    /// <summary>
    /// A string value indicating which compression strategy was used. The valid values are `exact_match` and `same_kind`
    /// </summary>
    public string CompressionStrategy { get; set; } = "exact_match";

    /// <summary>
    /// Count is the number of compressed spans the composite span represents. The minimum count is 2, as a composite span represents at least two spans.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Sum of the durations of all compressed spans this composite span represents in milliseconds.
    /// </summary>
    public double DurationSum { get; set; }
}