// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Elastic.OpenTelemetry.Configuration;
using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.Diagnostics;

internal readonly record struct DeferredLogEntry(LogLevel LogLevel, string Message);

/// <summary>
/// Used to capture log entries before the file logger is initialized.
/// </summary>
/// <remarks>
/// Log entries are stored in a concurrent queue and drained to the first file logger that is available.
/// Log entries are only captured if file logging is enabled in the options.
/// </remarks>
internal sealed class DeferredLogger : ILogger
{
	private readonly System.Timers.Timer _autoCloseTimer;

	private readonly ILogger? _additionalLogger;

	private static readonly Lock Lock = new();

	private ConcurrentQueue<DeferredLogEntry>? _logQueue = new();

	/// <summary>
	/// Create an instance of <see cref="DeferredLogger"/>.
	/// </summary>
	/// <param name="additionalLogger">An optional additional logger to which log entries will be written. For example,
	/// this may be available in ASP.NET Core scenarios and ensures the early log entries are sent to other sinks, such
	/// as the console, when available.</param>
	private DeferredLogger(ILogger? additionalLogger = null)
	{
		_additionalLogger = additionalLogger;

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

		_additionalLogger?.Log(logLevel, eventId, state, exception, formatter);

		var logLine = LogFormatter.Format(logLevel, eventId, state, exception, formatter);

		if (exception is not null)
			logLine = $"{logLine}{Environment.NewLine}{exception}";

		var logEntry = new DeferredLogEntry(logLevel, logLine);

		queue.Enqueue(logEntry);
	}

	internal void DrainAndRelease(StreamWriter streamWriter, LogLevel logLevel)
	{
		using (Lock.EnterScope())
		{
			if (_logQueue is null)
				return;

			while (_logQueue.TryDequeue(out var deferredLog))
			{
				if (logLevel <= deferredLog.LogLevel)
					streamWriter.WriteLine(deferredLog.Message);
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
		using (Lock.EnterScope())
		{
			return Instance ??= new DeferredLogger(options.AdditionalLogger);
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
