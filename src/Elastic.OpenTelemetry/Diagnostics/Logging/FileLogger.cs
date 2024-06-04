// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using System.Text;
using System.Threading.Channels;
using Elastic.OpenTelemetry.Configuration;
using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.Diagnostics.Logging;

internal sealed class FileLogger : IDisposable, IAsyncDisposable, ILogger
{
	private bool _disposing;
	private readonly ManualResetEventSlim _syncDisposeWaitHandle = new(false);
	private readonly StreamWriter? _streamWriter;
	private readonly Channel<string> _channel = Channel.CreateBounded<string>(new BoundedChannelOptions(1024)
	{
		SingleReader = true,
		SingleWriter = false,
		AllowSynchronousContinuations = true,
		FullMode = BoundedChannelFullMode.Wait
	});

	private readonly LogLevel _configuredLogLevel;

	public bool FileLoggingEnabled { get; }

	private readonly LoggerExternalScopeProvider _scopeProvider;

	public FileLogger(ElasticOpenTelemetryOptions options)
	{
		_scopeProvider = new LoggerExternalScopeProvider();

		var logDirectory = options.FileLogDirectory;
		var logLevel = options.FileLogLevel;
		if (logLevel == LogLevel.None || (logLevel == null && logDirectory == null))
			return;

		_configuredLogLevel = logLevel ?? LogLevel.Information;
		logDirectory ??= options.FileLogDirectoryDefault;

		var process = Process.GetCurrentProcess();
		// When ordered by filename, we get see logs from the same process grouped, then ordered by oldest to newest, then the PID for that instance
		var logFileName = $"{process.ProcessName}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{process.Id}.instrumentation.log";
		LogFilePath = Path.Combine(logDirectory, logFileName);

		if (!Directory.Exists(logDirectory))
			Directory.CreateDirectory(logDirectory);

		//StreamWriter.Dispose disposes underlying stream too
		var stream = new FileStream(LogFilePath, FileMode.OpenOrCreate, FileAccess.Write);
		_streamWriter = new StreamWriter(stream, Encoding.UTF8);

		WritingTask = Task.Run(async () =>
		{
			while (await _channel.Reader.WaitToReadAsync().ConfigureAwait(false) && !_disposing)
				while (_channel.Reader.TryRead(out var logLine) && !_disposing)
					await _streamWriter.WriteLineAsync(logLine).ConfigureAwait(false);

			_syncDisposeWaitHandle.Set();
		});

		_streamWriter.AutoFlush = true; // Ensure we don't lose logs by not flushing to the file.

		FileLoggingEnabled = true;
	}

	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		// We skip logging for any log level higher (numerically) than the configured log level
		if (!IsEnabled(logLevel))
			return;

		var logLine = LogFormatter.Format(logLevel, eventId, state, exception, formatter);

		var spin = new SpinWait();
		while (!_disposing)
		{
			if (_channel.Writer.TryWrite(logLine))
				break;
			spin.SpinOnce();
		}
	}

	public bool IsEnabled(LogLevel logLevel) => FileLoggingEnabled && _configuredLogLevel <= logLevel;

	public IDisposable BeginScope<TState>(TState state) where TState : notnull => _scopeProvider.Push(state);

	public string? LogFilePath { get; }

	public Task? WritingTask { get; }

	public void Dispose()
	{
		//tag that we are running a dispose this allows running tasks and spin waits to short circuit
		_disposing = true;
		_channel.Writer.TryComplete();

		_syncDisposeWaitHandle.Wait(TimeSpan.FromSeconds(1));

		_streamWriter?.Dispose();
	}

	public async ValueTask DisposeAsync()
	{
		//tag that we are running a dispose this allows running tasks and spin waits to short circuit
		_disposing = true;

		_channel.Writer.TryComplete();

		//Writing task will short circuit once _disposing is flipped to true
		if (WritingTask != null)
			await WritingTask.ConfigureAwait(false);

		_streamWriter?.Dispose();
	}
}
