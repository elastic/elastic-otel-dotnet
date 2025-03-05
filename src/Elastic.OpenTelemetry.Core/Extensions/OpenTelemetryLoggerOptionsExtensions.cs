// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace OpenTelemetry;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods for <see cref="OpenTelemetryLoggerOptions"/> used to register
/// the Elastic Distribution of OpenTelemetry (EDOT) .NET defaults.
/// </summary>
internal static class OpenTelemetryLoggerOptionsExtensions
{
	/// <summary>
	/// Ensures Elastic Distribution of OpenTelemetry (EDOT) .NET options are set for <see cref="OpenTelemetryLoggerOptions"/>.
	/// </summary>
	/// <param name="options">The <see cref="OpenTelemetryLoggerOptions"/> to configure.</param>
	/// <param name="logger">An <see cref="ILogger"/> to use for diagnostic logging.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="options"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="logger"/> is null.</exception>
	public static void WithElasticDefaults(this OpenTelemetryLoggerOptions options, ILogger logger)
	{
#if NET
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);
#else
		if (options is null)
			throw new ArgumentNullException(nameof(options));

		if (logger is null)
			throw new ArgumentNullException(nameof(logger));
#endif

		options.IncludeFormattedMessage = true;
		options.IncludeScopes = true;

		logger.LogConfiguredSignalProvider(nameof(Signals.Logs), nameof(OpenTelemetryLoggerOptions), "<n/a>");
	}
}
