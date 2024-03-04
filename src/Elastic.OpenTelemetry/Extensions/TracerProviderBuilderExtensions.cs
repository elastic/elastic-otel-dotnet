// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information
using Elastic.OpenTelemetry.Processors;
using OpenTelemetry.Trace;
using OpenTelemetry;
using System.Diagnostics;
using Elastic.OpenTelemetry.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Elastic.OpenTelemetry.Extensions;

/// <summary>
/// Provides Elastic APM extensions to <see cref="TracerProviderBuilder"/>.
/// </summary>
public static class TracerProviderBuilderExtensions
{
	/// <summary> Include Elastic APM Trace Processors to ensure data is enriched and extended.</summary>
	public static TracerProviderBuilder AddElasticProcessors(this TracerProviderBuilder builder, ILogger? logger = null) =>
		builder.LogAndAddProcessor(new TransactionIdProcessor(logger ?? NullLogger.Instance), logger ?? NullLogger.Instance);

	private static TracerProviderBuilder LogAndAddProcessor(this TracerProviderBuilder builder, BaseProcessor<Activity> processor, ILogger logger)
	{
		logger.LogProcessorAdded(processor.GetType().ToString(), builder.GetType().Name);
		return builder.AddProcessor(processor);
	}

	internal static TracerProviderBuilder LogAndAddSource(this TracerProviderBuilder builder, string sourceName, ILogger logger)
	{
		logger.LogSourceAdded(sourceName, builder.GetType().Name);
		return builder.AddSource(sourceName);
	}
}
