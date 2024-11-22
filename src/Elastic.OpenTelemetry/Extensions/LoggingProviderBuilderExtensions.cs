// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;
using static Elastic.OpenTelemetry.Configuration.Signals;

namespace Elastic.OpenTelemetry.Extensions;

/// <summary>
/// Elastic extensions for <see cref="LoggerProviderBuilder"/>.
/// </summary>
public static class LoggingProviderBuilderExtensions
{
	/// <summary>
	/// Use Elastic Distribution of OpenTelemetry .NET defaults for <see cref="LoggerProviderBuilder"/>.
	/// </summary>
	public static LoggerProviderBuilder UseElasticDefaults(this LoggerProviderBuilder builder, bool skipOtlp = false, ILogger? logger = null)
	{
		logger ??= NullLogger.Instance;

		if (!skipOtlp)
			builder.AddOtlpExporter();

		logger.LogConfiguredSignalProvider(nameof(Logs), nameof(LoggerProviderBuilder));
		return builder;
	}
}
