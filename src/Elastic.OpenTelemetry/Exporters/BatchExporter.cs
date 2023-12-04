using OpenTelemetry;

using System.Diagnostics;

namespace Elastic.OpenTelemetry.Exporters;

internal class BatchExporter : BaseExporter<Activity>
{
    public override ExportResult Export(in Batch<Activity> batch)
    {
        using var scope = SuppressInstrumentationScope.Begin();
        Console.WriteLine($"Exporting: {batch.Count:N0} items");
        return ExportResult.Success;
    }
}
