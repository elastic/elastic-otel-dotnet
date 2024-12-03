// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using Elastic.OpenTelemetry.Diagnostics;
using Elastic.OpenTelemetry.Processors;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;
using static Elastic.OpenTelemetry.Configuration.Signals;

namespace Elastic.OpenTelemetry.Extensions;

/// <summary>
/// Elastic extensions for <see cref="TracerProviderBuilder"/>.
/// </summary>
public static class TracerProviderBuilderExtensions
{
	/// <summary>
	/// Include Elastic trace processors to ensure data is enriched and extended.
	/// </summary>
	public static TracerProviderBuilder AddElasticProcessors(this TracerProviderBuilder builder, ILogger? logger = null)
	{
		logger ??= NullLogger.Instance;

		return builder
			.LogAndAddProcessor(new TransactionIdProcessor(logger), logger)
			.LogAndAddProcessor(new ElasticCompatibilityProcessor(logger), logger);
	}

	private static TracerProviderBuilder LogAndAddProcessor(this TracerProviderBuilder builder, BaseProcessor<Activity> processor, ILogger logger)
	{
		logger.LogProcessorAdded(processor.GetType().ToString(), builder.GetType().Name);
		return builder.AddProcessor(processor);
	}

	/// <summary>
	/// Use Elastic Distribution of OpenTelemetry .NET defaults for <see cref="TracerProviderBuilder"/>.
	/// </summary>
	public static TracerProviderBuilder UseElasticDefaults(this TracerProviderBuilder builder, bool skipOtlp = false, ILogger? logger = null)
	{
		logger ??= NullLogger.Instance;

		builder
			.AddHttpClientInstrumentation()
			.AddGrpcClientInstrumentation()
			.AddEntityFrameworkCoreInstrumentation()
			.AddElasticsearchClientInstrumentation()
			.AddSqlClientInstrumentation()
			.AddSource("Elastic.Transport")
			.AddElasticProcessors(logger);

		if (!skipOtlp)
			builder.AddOtlpExporter();

		logger.LogConfiguredSignalProvider(nameof(Traces), nameof(TracerProviderBuilder));
		return builder;
	}

	internal static TracerProviderBuilder UseAutoInstrumentationElasticDefaults(this TracerProviderBuilder builder, bool skipOtlp = false, ILogger? logger = null)
	{
		logger ??= NullLogger.Instance;

		builder
			.AddSource("Elastic.Transport")
			.AddElasticProcessors(logger);

		if (!skipOtlp)
			builder.AddOtlpExporter();

		logger.LogConfiguredSignalProvider(nameof(Traces), nameof(TracerProviderBuilder));
		return builder;
	}
}
