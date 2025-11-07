using System.Diagnostics;
using OpenTelemetry;

namespace Example.MinimalApi;

public class CustomProcessor : BaseProcessor<Activity>
{
	public override void OnEnd(Activity data)
	{
		Thread.Sleep(1000); // Enough time for the export to have happened

		data.SetTag("custom-processor-tag", "ProcessedByCustomProcessor");
	}
}
