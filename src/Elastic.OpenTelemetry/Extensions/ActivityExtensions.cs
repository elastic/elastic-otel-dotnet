using System.Diagnostics;

using Elastic.OpenTelemetry.Processors;

namespace Elastic.OpenTelemetry.Extensions;

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
