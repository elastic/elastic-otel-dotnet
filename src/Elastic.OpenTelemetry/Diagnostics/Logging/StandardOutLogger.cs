// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Configuration;
using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.Diagnostics.Logging;

internal sealed class StandardOutLogger(ElasticOpenTelemetryOptions options) : ILogger
{
	private readonly LogLevel _configuredLogLevel = options.LogLevel;

	private readonly LoggerExternalScopeProvider _scopeProvider = new();

	private bool StandardOutLoggingEnabled { get; } = options.GlobalLogEnabled && options.LogTargets.HasFlag(LogTargets.StdOut);

	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		// We skip logging for any log level higher (numerically) than the configured log level
		if (!IsEnabled(logLevel))
			return;

		var logLine = LogFormatter.Format(logLevel, eventId, state, exception, formatter);
		Console.WriteLine(logLine);

	}

	public bool IsEnabled(LogLevel logLevel) => StandardOutLoggingEnabled && _configuredLogLevel <= logLevel;

	public IDisposable BeginScope<TState>(TState state) where TState : notnull => _scopeProvider.Push(state);
}
