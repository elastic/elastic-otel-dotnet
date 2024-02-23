// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;
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

	private static readonly LogLevel ConfiguredLogLevel = AgentLoggingHelpers.GetElasticOtelLogLevel();

	public bool FileLoggingEnabled { get; } = AgentLoggingHelpers.IsFileLoggingEnabled();

	private readonly LoggerExternalScopeProvider _scopeProvider;

	private FileLogger()
	{
		_scopeProvider = new LoggerExternalScopeProvider();
		var process = Process.GetCurrentProcess();

		if (!FileLoggingEnabled) return;

		var configuredPath = Environment.GetEnvironmentVariable(EnvironmentVariables.ElasticOtelLogDirectoryEnvironmentVariable);

		// Defaults to local application data (C:\Users\{username}\AppData\Local\Elastic on Windows; $HOME/.local/share/elastic on Linux)
		var path = string.IsNullOrEmpty(configuredPath)
			? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), GetElasticFolder)
			: configuredPath;

		// When ordered by filename, we get see logs from the same process grouped, then ordered by oldest to newest, then the PID for that instance
		LogFilePath = Path.Combine(path, $"{process.ProcessName}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{process.Id}.agent.log");

		if (!Directory.Exists(path))
			Directory.CreateDirectory(path);

		_streamWriter = new StreamWriter(LogFilePath, Encoding.UTF8, new FileStreamOptions
		{
			Access = FileAccess.Write,
			Mode = FileMode.OpenOrCreate,
		});

		WritingTask = Task.Run(async () =>
		{
			while (await _channel.Reader.WaitToReadAsync().ConfigureAwait(false) && !_disposing)
			while (_channel.Reader.TryRead(out var logLine) && !_disposing)
				await _streamWriter.WriteLineAsync(logLine).ConfigureAwait(false);

			_syncDisposeWaitHandle.Set();
		});

		_streamWriter.AutoFlush = true; // Ensure we don't lose logs by not flushing to the file.
	}

	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		// We skip logging for any log level higher (numerically) than the configured log level
		if (logLevel > ConfiguredLogLevel)
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

	public bool IsEnabled(LogLevel logLevel) => FileLoggingEnabled && ConfiguredLogLevel <= logLevel;

	public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _scopeProvider.Push(state);

	public static FileLogger Instance { get; } = new();

	public string? LogFilePath { get; }

	public Task? WritingTask { get; }

	private static string GetElasticFolder => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Elastic" : "elastic";

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

		if (_streamWriter != null)
			await _streamWriter.DisposeAsync().ConfigureAwait(false);
	}

}
