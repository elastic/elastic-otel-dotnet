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

	private bool _disposed;

	public bool FileLoggingEnabled { get; }

	private readonly LoggerExternalScopeProvider _scopeProvider;

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

			// When ordered by filename, we see logs from the same process grouped, then ordered by oldest to newest, then the PID for that instance
			var logFileName = $"EDOT.{process.ProcessName}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{process.Id}.log";
			var logDirectory = options.LogDirectory;

			LogFilePath = Path.Combine(logDirectory, logFileName);

			if (!Directory.Exists(logDirectory))
				Directory.CreateDirectory(logDirectory);

			// StreamWriter.Dispose disposes underlying stream too.
			var stream = new FileStream(LogFilePath, FileMode.OpenOrCreate, FileAccess.Write);

			_streamWriter = new StreamWriter(stream, Encoding.UTF8);

			_streamWriter.WriteLine("DateTime (UTC)           Thread  SpanId  Level         Message");
			_streamWriter.WriteLine();

			WritingTask = Task.Run(async () =>
			{
				var cancellationToken = _cancellationTokenSource.Token;

				while (!cancellationToken.IsCancellationRequested)
				{
					await _logSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

					while (_logQueue.TryDequeue(out var logEntry))
					{
						await _streamWriter.WriteLineAsync(logEntry).ConfigureAwait(false);
					}
				}

				// Flush remaining log entries before exiting
				while (_logQueue.TryDequeue(out var logEntry))
				{
					await _streamWriter.WriteLineAsync(logEntry).ConfigureAwait(false);
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
		if (_disposed)
			return;

		_cancellationTokenSource.Cancel();
		_logSemaphore.Release();

		WritingTask?.Wait();

		_streamWriter.Dispose();
		_logSemaphore.Dispose();
		_cancellationTokenSource.Dispose();

		_disposed = true;
	}

	public async ValueTask DisposeAsync()
	{
		if (_disposed)
			return;

		_cancellationTokenSource.Cancel();
		_logSemaphore.Release();

		await WritingTask.ConfigureAwait(false);

		_streamWriter.Dispose();
		_logSemaphore.Dispose();
		_cancellationTokenSource.Dispose();

		_disposed = true;
	}
}
