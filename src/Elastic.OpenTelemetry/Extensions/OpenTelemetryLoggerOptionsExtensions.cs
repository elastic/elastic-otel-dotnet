// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry.Logs;
using static Elastic.OpenTelemetry.Configuration.ElasticOpenTelemetryOptions.Signals;

namespace Elastic.OpenTelemetry.Extensions;

/// <summary>
/// Elastic extensions for <see cref="OpenTelemetryLoggerOptions"/>.
/// </summary>
public static class OpenTelemetryLoggerOptionsExtensions
{
	/// <summary>
	/// Ensures Elastic distro options are set for <see cref="OpenTelemetryLoggerOptions"/>
	/// </summary>
	public static void UseElasticDefaults(this OpenTelemetryLoggerOptions options, ILogger? logger = null)
	{
		logger ??= NullLogger.Instance;
		options.IncludeFormattedMessage = true;
		options.IncludeScopes = true;
		logger.LogConfiguredSignalProvider(nameof(Logging), nameof(OpenTelemetryLoggerOptions));
	}
}
