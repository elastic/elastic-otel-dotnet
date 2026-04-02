// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Runtime.CompilerServices;
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
	internal static void WithElasticDefaults(this OpenTelemetryLoggerOptions options, ILogger logger)
	{
		// We don't capture the stack trace here as we'll have that logged deeper in the call stack if needed.
		if (BootstrapLogger.IsEnabled)
			BootstrapLogger.Log($"{nameof(OpenTelemetryLoggerOptionsExtensions)}.{nameof(WithElasticDefaults)}(this OpenTelemetryLoggerOptions options, ILogger logger) invoked " +
				$"on options with object hash '{RuntimeHelpers.GetHashCode(options)}'.");

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

		// NOTE: The OTLP exporter may emit duplicate attributes when IncludeScopes is enabled, as scopes are emitted as attributes on the log record.
		// This is not strictly spec compliant. When using EDOT collector or the managed OTLP endpoint, these duplicate attributes will be deduplicated.
		// This works by including the first occurrence of the attribute and ignoring subsequent occurrences with the same key.
		options.IncludeScopes = true;

		logger.LogConfiguredSignalProvider(nameof(Signals.Logs), nameof(OpenTelemetryLoggerOptions), "<n/a>");
	}
}
