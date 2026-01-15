// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Elastic.OpenTelemetry.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Elastic.OpenTelemetry.Diagnostics;

internal readonly record struct DeferredLogEntry(
	LogLevel LogLevel,
	EventId EventId,
	object? State,
	Exception? Exception,
	Func<object?, Exception?, string> Formatter
);

/// <summary>
/// Used to capture log entries before the file logger is initialized.
/// </summary>
/// <remarks>
/// The logger instance is static and shared across the application domain.
/// The deferral period is limited to 10 seconds after the first use of the logger.
/// After this period, if the logger has not been drained, it will automatically release its resources.
/// </remarks>
internal sealed class DeferredLogger : ILogger
{
	private readonly System.Timers.Timer _autoCloseTimer;

	private static readonly Lock Lock = new();

	private ConcurrentQueue<DeferredLogEntry>? _logQueue = new();

	/// <summary>
	/// Create an instance of <see cref="DeferredLogger"/>.
	/// </summary>
	private DeferredLogger()
	{
		_autoCloseTimer = new System.Timers.Timer(10_000); // 10 seconds in milliseconds
		_autoCloseTimer.Elapsed += (_, _) =>
		{
			using (Lock.EnterScope())
			{
				if (_logQueue is null)
					return;

				// Only clear the static instance if it's still pointing to this instance
				if (ReferenceEquals(Instance, this))
					Instance = null;

				_logQueue = null;
			}
			
			// Dispose timer outside the lock to avoid potential deadlocks
			_autoCloseTimer.Stop();
			_autoCloseTimer.Dispose();
		};
		_autoCloseTimer.AutoReset = false;
		_autoCloseTimer.Start();
	}

	public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

	public bool IsEnabled(LogLevel logLevel) => _logQueue is not null;

	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		var queue = _logQueue;

		if (queue is null)
			return;

		var logEntry = new DeferredLogEntry(logLevel, eventId, state, exception, BoxedFormatter);

		queue.Enqueue(logEntry);

		// While not ideal, boxing is the only way to store the state for deferred logging
		// and should be suitable for the short period needed before this logger is drained.
		string BoxedFormatter(object? s, Exception? e) => formatter((TState)s!, e);
	}

	internal void DrainAndRelease(ILogger logger)
	{
		using (Lock.EnterScope())
		{
			if (_logQueue is null)
				return;

			while (_logQueue.TryDequeue(out var deferredLog))
			{
				logger.Log(
					deferredLog.LogLevel,
					deferredLog.EventId,
					deferredLog.State,
					deferredLog.Exception,
					deferredLog.Formatter);
			}

			// Only clear the static instance if it's still pointing to this instance
			if (ReferenceEquals(Instance, this))
				Instance = null;
				
			_logQueue = null;
		}
		
		// Stop and dispose the timer outside the lock
		_autoCloseTimer.Stop();
		_autoCloseTimer.Dispose();
	}

	private static DeferredLogger? Instance;

	internal static ILogger GetOrCreate(CompositeElasticOpenTelemetryOptions options)
	{
		// We can skip creating the deferred logger if file logging is disabled in the local configuration
		// and OpAmp is not enabled. When OpAmp is enabled, we may need to log the earlier log entries once
		// we have a log level from central config.
		if (options.GlobalLogEnabled is false && options.IsOpAmpEnabled() is false)
			return NullLogger.Instance;

		using (Lock.EnterScope())
		{
			return Instance ??= new DeferredLogger();
		}
	}

	internal static bool TryGetInstance([NotNullWhen(true)] out DeferredLogger? instance)
	{
		instance = Instance;
		return instance is not null;
	}

	private class NullScope : IDisposable
	{
		public static readonly NullScope Instance = new();

		public void Dispose()
		{
			// No-op
		}
	}
}
