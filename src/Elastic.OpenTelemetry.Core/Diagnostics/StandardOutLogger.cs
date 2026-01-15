// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Configuration;
using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.Diagnostics;

internal sealed class StandardOutLogger(CompositeElasticOpenTelemetryOptions options) : ILogger
{
	private readonly CompositeElasticOpenTelemetryOptions _options = options;
	private readonly LoggerExternalScopeProvider _scopeProvider = new();

	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		if (!IsEnabled(logLevel))
			return;

		var logLine = LogFormatter.Format(logLevel, eventId, state, exception, formatter);

		if (logLevel > LogLevel.Warning)
			Console.Error.WriteLine(logLine);
		else
			Console.Out.WriteLine(logLine);
	}

	// We skip logging for any log level higher (numerically) than the configured log level
	public bool IsEnabled(LogLevel logLevel) => _options.GlobalLogEnabled && _options.LogTargets.HasFlag(LogTargets.StdOut) && _options.LogLevel <= logLevel;

	public IDisposable BeginScope<TState>(TState state) where TState : notnull => _scopeProvider.Push(state);
}
