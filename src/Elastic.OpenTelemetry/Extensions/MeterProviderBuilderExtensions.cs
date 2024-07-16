// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry.Metrics;
using static Elastic.OpenTelemetry.Configuration.ElasticOpenTelemetryOptions.Signals;

namespace Elastic.OpenTelemetry.Extensions;

/// <summary>
/// Elastic extensions for <see cref="MeterProviderBuilder"/>.
/// </summary>
public static class MeterProviderBuilderExtensions
{
	/// <summary> Use Elastic Distribution for OpenTelemetry .NET defaults for <see cref="MeterProviderBuilder"/> </summary>
	public static MeterProviderBuilder UseElasticDefaults(this MeterProviderBuilder builder, ILogger? logger = null)
	{
		logger ??= NullLogger.Instance;

		builder
			.AddProcessInstrumentation()
			.AddRuntimeInstrumentation()
			.AddHttpClientInstrumentation();

		logger.LogConfiguredSignalProvider(nameof(Metrics), nameof(MeterProviderBuilder));

		return builder;
	}
}
