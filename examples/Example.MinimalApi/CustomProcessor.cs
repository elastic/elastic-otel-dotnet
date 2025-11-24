// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

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
