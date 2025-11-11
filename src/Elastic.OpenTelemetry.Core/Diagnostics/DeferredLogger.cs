// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Elastic.OpenTelemetry.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Elastic.OpenTelemetry.Diagnostics;

/// <summary>
/// Used to capture log entries before the file logger is initialized.
/// </summary>
/// <remarks>
/// Log entries are stored in a concurrent queue and drained to the first file logger that is available.
/// Log entries are only captured if file logging is enabled in the options.
/// </remarks>
internal sealed class DeferredLogger : ILogger
{
	private readonly bool _isEnabled = false;
	private readonly LogLevel _configuredLogLevel;
	private readonly ConcurrentQueue<string> _logQueue = new();
	private readonly ILogger? _additionalLogger;

	/// <summary>
	/// Create an instance of <see cref="DeferredLogger"/>.
	/// </summary>
	/// <param name="options">The options used to configure the logger.</param>
	/// <param name="additionalLogger">An optional additional logger to which log entries will be written. For example,
	/// this may be available in ASP.NET Core scenarios and ensures the early log entries are sent to other sinks, such
	/// as the console, when available.</param>
	private DeferredLogger(CompositeElasticOpenTelemetryOptions options, ILogger? additionalLogger = null)
	{
		_isEnabled = options.GlobalLogEnabled && options.LogTargets.HasFlag(LogTargets.File);
		_configuredLogLevel = options.LogLevel;

		if (!_isEnabled)
			return;

		_additionalLogger = additionalLogger;
	}

	public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

	public bool IsEnabled(LogLevel logLevel) => _isEnabled && _configuredLogLevel <= logLevel;

	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		if (!IsEnabled(logLevel))
			return;

		var logLine = LogFormatter.Format(logLevel, eventId, state, exception, formatter);

		if (exception is not null)
			logLine = $"{logLine}{Environment.NewLine}{exception}";

		_logQueue.Enqueue(logLine);

		_additionalLogger?.Log(logLevel, eventId, state, exception, formatter);
	}

	internal void DrainLogQueue(StreamWriter streamWriter)
	{
		while (_logQueue.TryDequeue(out var deferredLog))
		{
			streamWriter.WriteLine(deferredLog);
		}
	}

	private static DeferredLogger? Instance;

	internal static ILogger GetOrCreate(CompositeElasticOpenTelemetryOptions options)
	{
		// We want, at most, one instance of DeferredLogger for the application.
		// We only create a DeferredFileLogger if file logging is enabled
		if (options.GlobalLogEnabled && options.LogTargets.HasFlag(LogTargets.File))
			return Instance ??= new DeferredLogger(options, options.AdditionalLogger);

		return NullLogger.Instance;
	}

	internal static bool TryGetInstance([NotNullWhen(true)] out DeferredLogger? instance)
	{
		instance = Instance;
		return instance is not null;
	}

	internal static void ReleaseInstance() => Instance = null;

	private class NullScope : IDisposable
	{
		public static readonly NullScope Instance = new();

		public void Dispose()
		{
			// No-op
		}
	}
}
