// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information
using Elastic.OpenTelemetry.Processors;
using OpenTelemetry.Trace;
using OpenTelemetry;
using System.Diagnostics;
using Elastic.OpenTelemetry.Diagnostics;

using static Elastic.OpenTelemetry.Diagnostics.ElasticOpenTelemetryDiagnostics;

namespace Elastic.OpenTelemetry.Extensions;

/// <summary> Provides Elastic APM extensions to <see cref="TracerProviderBuilder"/> </summary>
public static class TraceBuilderProviderExtensions
{
	/// <summary> Include Elastic APM Trace Processors to ensure data is enriched and extended.</summary>
	public static TracerProviderBuilder AddElasticProcessors(this TracerProviderBuilder builder) =>
		builder.LogAndAddProcessor(new TransactionIdProcessor());

	internal static TracerProviderBuilder LogAndAddProcessor(this TracerProviderBuilder builder, BaseProcessor<Activity> processor)
	{
		Log(ProcessorAddedEvent, () => new DiagnosticEvent<AddProcessorPayload>(new(processor.GetType(), builder.GetType())));
		return builder.AddProcessor(processor);
	}

	internal static TracerProviderBuilder LogAndAddSource(this TracerProviderBuilder builder, string sourceName)
	{
		Log(SourceAddedEvent, () => new DiagnosticEvent<AddSourcePayload>(new(sourceName, builder.GetType())));
		return builder.AddSource(sourceName);
	}
}
