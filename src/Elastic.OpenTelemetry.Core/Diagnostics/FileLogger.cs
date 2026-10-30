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
	internal static readonly string FileNamePrefix = "edot-dotnet-";
	internal static readonly string FileNameSuffix;

	static FileLogger()
	{
		var process = Process.GetCurrentProcess();

		if (process is null)
		{
			FileNameSuffix = $"unknown-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmssfffZ}.log";
			return;
		}

		FileNameSuffix = $"{process.Id}-{process.ProcessName}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmssfffZ}.log";
	}

	private static readonly TimeSpan DefaultDisposeTimeout = TimeSpan.FromSeconds(5);

	private readonly ConcurrentQueue<string> _logQueue = new();
	private readonly SemaphoreSlim _logSemaphore = new(0);
	private readonly CancellationTokenSource _cancellationTokenSource = new();
	private readonly StreamWriter _streamWriter;
	private readonly LoggerExternalScopeProvider _scopeProvider;
	private readonly CompositeElasticOpenTelemetryOptions _options;
	private readonly TimeSpan _disposeTimeout;
	private readonly bool _fileLoggingEnabled;

	private int _disposed;
	private volatile bool _writingFailed;

	internal Guid InstanceId { get; } = Guid.NewGuid();

	public bool FileLoggingEnabled => _fileLoggingEnabled;

	public FileLogger(CompositeElasticOpenTelemetryOptions options)
		: this(options, DefaultDisposeTimeout, null)
	{
	}

	// Allows tests to substitute the destination stream and shorten the dispose timeout.
	internal FileLogger(CompositeElasticOpenTelemetryOptions options, TimeSpan disposeTimeout, Func<string, Stream>? createStream)
	{
		if (BootstrapLogger.IsEnabled)
		{
			BootstrapLogger.LogWithStackTrace($"{nameof(FileLogger)}: Instance '{InstanceId}' created via ctor." +
				$"{Environment.NewLine}    Invoked with `{nameof(CompositeElasticOpenTelemetryOptions)}` instance '{options.InstanceId}'.");
		}

		_scopeProvider = new LoggerExternalScopeProvider();
		_options = options;
		_disposeTimeout = disposeTimeout;

		// Making this decision here is okay for now, based on how we activate the composite logger.
		// When we need to make options dynamic from central config, we willl need to revisit this to implement a more dynamic approach
		// which will also need to create the stream etc if file logging was previously disabled.
		_fileLoggingEnabled = _options.GlobalLogEnabled && _options.LogTargets.HasFlag(LogTargets.File);

		_streamWriter = StreamWriter.Null;

		WritingTask = Task.CompletedTask;

		if (!FileLoggingEnabled)
			return;

		try
		{
			// This naming resembles the naming structure for OpenTelemetry log files.
			var logFileName = $"{FileNamePrefix}{FileNameSuffix}";
			var logDirectory = options.LogDirectory;

			LogFilePath = Path.Combine(logDirectory, logFileName);

			if (!Directory.Exists(logDirectory))
				Directory.CreateDirectory(logDirectory);

			// StreamWriter.Dispose disposes underlying stream too.
			var stream = createStream?.Invoke(LogFilePath) ?? new FileStream(LogFilePath, FileMode.Append, FileAccess.Write, FileShare.Read);

			_streamWriter = new StreamWriter(stream, Encoding.UTF8);

			_streamWriter.WriteLine("DateTime (UTC)                Thread  SpanId  Level         Message");
			_streamWriter.WriteLine();

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
					if (!await DrainQueueAsync().ConfigureAwait(false))
						return;
				}

				// Flush remaining log entries before exiting. Dispose waits for this task before
				// disposing the stream, so entries queued prior to shutdown are not lost.
				await DrainQueueAsync().ConfigureAwait(false);
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
			BootstrapLogger.Log($"{nameof(FileLogger)}: [ERROR] An exception occurred while initializing the file logger: {ex.Message}.");

			if (options?.AdditionalLogger is not null)
				options?.AdditionalLogger.LogError(new EventId(530, "FileLoggingFailure"), ex, "Failed to set up file logging due to exception: {ExceptionMessage}.", ex.Message);
			else
				Console.Error.WriteLine($"Failed to set up file logging due to exception: {ex.Message}.");
		}

		// If we fall through the `try` block, consider file logging disabled.
		_fileLoggingEnabled = false;
	}

	private async Task<bool> DrainQueueAsync()
	{
		while (_logQueue.TryDequeue(out var logEntry))
		{
			try
			{
				await _streamWriter.WriteLineAsync(logEntry).ConfigureAwait(false);
			}
			catch (ObjectDisposedException)
			{
				return false;
			}
			catch (Exception ex)
			{
				OnWriteFailed(ex);
				return false;
			}
		}

		return true;
	}

	private void OnWriteFailed(Exception ex)
	{
		// Once a write fails (e.g. disk full), stop accepting entries. Nothing will drain the queue
		// after the writing task exits, so continuing to enqueue would grow memory without bound.
		_writingFailed = true;

		while (_logQueue.TryDequeue(out _))
		{
			// Discard entries that can no longer be written.
		}

		BootstrapLogger.Log($"{nameof(FileLogger)}: [ERROR] An exception occurred while writing to the log file: {ex.Message}.");

		if (_options.AdditionalLogger is not null)
			_options.AdditionalLogger.LogError(new EventId(530, "FileLoggingFailure"), ex, "File logging disabled after failing to write to '{LogFilePath}': {ExceptionMessage}.", LogFilePath, ex.Message);
		else
			Console.Error.WriteLine($"File logging disabled after failing to write to '{LogFilePath}': {ex.Message}.");
	}

	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		// We skip logging for any log level higher (numerically) than the configured log level
		if (!IsEnabled(logLevel) || Volatile.Read(ref _disposed) == 1)
			return;

		var logLine = LogFormatter.Format(logLevel, eventId, state, exception, formatter);

		if (exception is not null)
			logLine = $"{logLine}{Environment.NewLine}{exception}";

		_logQueue.Enqueue(logLine);

		try
		{
			_logSemaphore.Release();
		}
		catch (ObjectDisposedException)
		{
			// Lost a race with Dispose; the entry is dropped.
		}
	}

	public bool IsEnabled(LogLevel logLevel) => FileLoggingEnabled && !_writingFailed && _options.LogLevel <= logLevel;

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
			if (!WritingTask.Wait(_disposeTimeout))
			{
				DisposeAfterWritingCompletes();
				return;
			}
		}
		catch (Exception ex)
		{
			// Not expected, as the writing task handles its own failures, but disposal must not throw.
			BootstrapLogger.Log($"{nameof(FileLogger)}: [ERROR] The writing task failed: {ex.GetBaseException().Message}.");
		}

		DisposeResources();
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
			await WritingTask.WaitAsync(_disposeTimeout).ConfigureAwait(false);
		}
		catch (TimeoutException)
		{
			DisposeAfterWritingCompletes();
			return;
		}
		catch (Exception ex)
		{
			// Not expected, as the writing task handles its own failures, but disposal must not throw.
			BootstrapLogger.Log($"{nameof(FileLogger)}: [ERROR] The writing task failed: {ex.Message}.");
		}

		// Synchronous disposal is sufficient: AutoFlush means nothing remains buffered by this point.
		DisposeResources();
	}

	private void DisposeAfterWritingCompletes() =>
		// Schedule cleanup even if the writing task completed just as the timeout elapsed.
		_ = WritingTask.ContinueWith(_ => DisposeResources(), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);

	private void DisposeResources()
	{
		try
		{
			_streamWriter.Dispose();
		}
		catch (Exception ex)
		{
			BootstrapLogger.Log($"{nameof(FileLogger)}: [ERROR] An exception occurred while disposing the file logger: {ex.Message}.");
		}
		finally
		{
			_logSemaphore.Dispose();
			_cancellationTokenSource.Dispose();
		}
	}
}
