// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Elastic.OpenTelemetry.Configuration;
using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.Diagnostics;

internal sealed class FileLogger : IDisposable, IAsyncDisposable, ILogger
{
	private readonly ConcurrentQueue<string> _logQueue = new();
	private readonly SemaphoreSlim _logSemaphore = new(0);
	private readonly CancellationTokenSource _cancellationTokenSource = new();
	private readonly StreamWriter _streamWriter;
	private readonly LogLevel _configuredLogLevel;
	private readonly LoggerExternalScopeProvider _scopeProvider;

	private int _disposed;

	public bool FileLoggingEnabled { get; }

	public FileLogger(CompositeElasticOpenTelemetryOptions options)
	{
		_scopeProvider = new LoggerExternalScopeProvider();
		_configuredLogLevel = options.LogLevel;
		_streamWriter = StreamWriter.Null;

		WritingTask = Task.CompletedTask;
		FileLoggingEnabled = options.GlobalLogEnabled && options.LogTargets.HasFlag(LogTargets.File);

		if (!FileLoggingEnabled)
			return;

		try
		{
			var process = Process.GetCurrentProcess();

			// This naming resembles the naming structure for OpenTelemetry log files.
			var logFileName = $"edot-dotnet-{process.Id}-{process.ProcessName}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmssfffZ}.log";
			var logDirectory = options.LogDirectory;

			LogFilePath = Path.Combine(logDirectory, logFileName);

			if (!Directory.Exists(logDirectory))
				Directory.CreateDirectory(logDirectory);

			// StreamWriter.Dispose disposes underlying stream too.
			var stream = new FileStream(LogFilePath, FileMode.Append, FileAccess.Write, FileShare.Read);

			_streamWriter = new StreamWriter(stream, Encoding.UTF8);

			_streamWriter.WriteLine("DateTime (UTC)           Thread  SpanId  Level         Message");
			_streamWriter.WriteLine();

			// Drain any deferred log entries captured before the file logger was initialized.
			// These appear before the preamble to ensure correct timestamping order.
			if (DeferredLogger.TryGetInstance(out var deferredLogger))
			{
				deferredLogger.DrainAndRelease(_streamWriter);
			}

			WritingTask = Task.Run(async () =>
			{
				var cancellationToken = _cancellationTokenSource.Token;

				while (true)
				{
					try
					{
						await _logSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
					}
					catch (OperationCanceledException)
					{
						// Cancellation requested, exit the loop
						break;
					}

					// Process all queued log entries
					while (_logQueue.TryDequeue(out var logEntry))
					{
						// Check if disposal has started before writing
						if (Volatile.Read(ref _disposed) == 1)
							return;

						try
						{
							await _streamWriter.WriteLineAsync(logEntry).ConfigureAwait(false);
						}
						catch (ObjectDisposedException)
						{
							// Stream was disposed during shutdown, exit gracefully
							return;
						}
					}
				}

				// Flush remaining log entries before exiting
				while (_logQueue.TryDequeue(out var logEntry))
				{
					// Check if disposal has started before writing
					if (Volatile.Read(ref _disposed) == 1)
						return;

					try
					{
						await _streamWriter.WriteLineAsync(logEntry).ConfigureAwait(false);
					}
					catch (ObjectDisposedException)
					{
						// Stream was disposed, exit gracefully
						return;
					}
				}
			});

			_streamWriter.AutoFlush = true; // Ensure we don't lose logs by not flushing to the file.

			if (options?.AdditionalLogger is not null)
				options?.AdditionalLogger.LogInformation("File logging for EDOT .NET enabled. Logs are being written to '{LogFilePath}'.", LogFilePath);
			else
				Console.Out.WriteLine($"File logging for EDOT .NET enabled. Logs are being written to '{LogFilePath}'.");

			return;
		}
		catch (Exception ex)
		{
			if (options?.AdditionalLogger is not null)
				options?.AdditionalLogger.LogError(new EventId(530, "FileLoggingFailure"), ex, "Failed to set up file logging due to exception: {ExceptionMessage}.", ex.Message);
			else
				Console.Error.WriteLine($"Failed to set up file logging due to exception: {ex.Message}.");
		}

		// If we fall through the `try` block, consider file logging disabled.
		FileLoggingEnabled = false;
	}

	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		// We skip logging for any log level higher (numerically) than the configured log level
		if (!IsEnabled(logLevel))
			return;

		var logLine = LogFormatter.Format(logLevel, eventId, state, exception, formatter);

		if (exception is not null)
			logLine = $"{logLine}{Environment.NewLine}{exception}";

		_logQueue.Enqueue(logLine);
		_logSemaphore.Release();
	}

	public bool IsEnabled(LogLevel logLevel) => FileLoggingEnabled && _configuredLogLevel <= logLevel;

	public IDisposable BeginScope<TState>(TState state) where TState : notnull => _scopeProvider.Push(state);

	public string? LogFilePath { get; }

	public Task WritingTask { get; }

	public void Dispose()
	{
		if (Interlocked.Exchange(ref _disposed, 1) != 0)
			return;

		_cancellationTokenSource.Cancel();
		_logSemaphore.Release();

		try
		{
			// Wait for the writing task to complete with a timeout to prevent hanging
			WritingTask?.Wait(TimeSpan.FromSeconds(5));
		}
		catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
		{
			// Expected when cancellation token is triggered
		}
		catch (OperationCanceledException)
		{
			// Expected when cancellation token is triggered
		}

		_streamWriter.Dispose();
		_logSemaphore.Dispose();
		_cancellationTokenSource.Dispose();
	}

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _disposed, 1) != 0)
			return;

		_cancellationTokenSource.Cancel();
		_logSemaphore.Release();

		try
		{
			// Wait for the writing task to complete with a timeout to prevent hanging
			await WritingTask.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
		}
		catch (TimeoutException)
		{
			// Task didn't complete in time, proceed with disposal
		}
		catch (OperationCanceledException)
		{
			// Expected when cancellation token is triggered
		}

#if NET
        await _streamWriter.DisposeAsync().ConfigureAwait(false);
#else
		_streamWriter.Dispose();
#endif
		_logSemaphore.Dispose();
		_cancellationTokenSource.Dispose();
	}
}
