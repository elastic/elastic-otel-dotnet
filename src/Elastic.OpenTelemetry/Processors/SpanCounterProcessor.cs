// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information
using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry;

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
