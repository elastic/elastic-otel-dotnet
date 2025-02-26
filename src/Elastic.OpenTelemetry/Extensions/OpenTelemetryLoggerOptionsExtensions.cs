// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry.Logs;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace OpenTelemetry;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods for <see cref="OpenTelemetryLoggerOptions"/> used to register
/// the Elastic Distribution of OpenTelemetry (EDOT) defaults.
/// </summary>
public static class OpenTelemetryLoggerOptionsExtensions
{
	/// <summary>
	/// Ensures Elastic Distribution of OpenTelemetry (EDOT) options are set for <see cref="OpenTelemetryLoggerOptions"/>
	/// </summary>
	public static void WithElasticDefaults(this OpenTelemetryLoggerOptions options, ILogger? logger = null)
	{
		logger ??= NullLogger.Instance;
		options.IncludeFormattedMessage = true;
		options.IncludeScopes = true;
		logger.LogConfiguredSignalProvider(nameof(Signals.Logs), nameof(OpenTelemetryLoggerOptions));
	}
}
