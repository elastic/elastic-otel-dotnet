// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information
using System.Diagnostics;
using OpenTelemetry;

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
