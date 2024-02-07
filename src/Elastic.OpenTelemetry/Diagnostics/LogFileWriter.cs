// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;

namespace Elastic.OpenTelemetry.Diagnostics;

internal sealed class LogFileWriter : IDisposable, IAsyncDisposable
{
	public static readonly bool FileLoggingEnabled = IsFileLoggingEnabled();

	private readonly StreamWriter _streamWriter;
	private readonly Channel<string> _channel = Channel.CreateBounded<string>(new BoundedChannelOptions(1024)
	{
		SingleReader = true,
		SingleWriter = false,
		AllowSynchronousContinuations = true,
		FullMode = BoundedChannelFullMode.Wait
	});

	private LogFileWriter()
	{
		var process = Process.GetCurrentProcess();

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
			while (await _channel.Reader.WaitToReadAsync().ConfigureAwait(false))
			{
				while (_channel.Reader.TryRead(out var logLine))
				{
					await _streamWriter.WriteLineAsync(logLine).ConfigureAwait(false);
				}
			}
		});

		string[] preAmble = [
			$"Elastic OpenTelemetry Distribution: {Agent.InformationalVersion}",
			$"Process ID: {process.Id}",
			$"Process started: {process.StartTime.ToUniversalTime():yyyy-MM-dd HH:mm:ss.fff}",
		];

		var builder = StringBuilderCache.Acquire();

		foreach (var item in preAmble)
		{
			WriteLogPrefix(DiagnosticErrorLevels.Info, builder);
			builder.Append(item);
			_streamWriter.WriteLine(builder.ToString());
			builder.Clear();
		}

		_streamWriter.Flush();
		_streamWriter.AutoFlush = true; // Ensure we don't lose logs by not flushing to the file.

		StringBuilderCache.Release(builder);
	}

	private static void WriteLogPrefix(string logLevel, StringBuilder builder) =>
		WriteLogPrefix(Environment.CurrentManagedThreadId, DateTime.UtcNow, logLevel, builder);

	private static void WriteLogPrefix(int managedThreadId, DateTime dateTime, string level, StringBuilder builder)
	{
		builder.Append('[')
			.Append(dateTime.ToString("yyyy-MM-dd HH:mm:ss.fff"))
			.Append("][")
			.Append(managedThreadId == -1 ? "-" : managedThreadId)
			.Append("][")
			.Append(level)
			.Append(']');

		var length = builder.Length;
		var padding = 40 - length;

		for (var i = 0; i < padding; i++)
		{
			builder.Append(' ');
		}
	}

	public static LogFileWriter Instance { get; } = new();

	public string LogFilePath { get; }

	public Task WritingTask { get; }

	public void WriteErrorLogLine(IDiagnosticEvent diagnosticEvent, string message) =>
		WriteLogLine(diagnosticEvent, DiagnosticErrorLevels.Error, message);

	public void WriteInfoLogLine(IDiagnosticEvent diagnosticEvent, string message) =>
		WriteLogLine(diagnosticEvent, DiagnosticErrorLevels.Info, message);

	public void WriteTraceLogLine(IDiagnosticEvent diagnosticEvent, string message) =>
		WriteLogLine(diagnosticEvent, DiagnosticErrorLevels.Trace, message);

	public void WriteLogLine(IDiagnosticEvent diagnosticEvent, string logLevel, string message) =>
		WriteLogLine(diagnosticEvent.Activity, diagnosticEvent.ManagedThreadId, diagnosticEvent.DateTime, logLevel, message);

	public void WriteLogLine(Activity? activity, int managedThreadId, DateTime dateTime, string logLevel, string logLine)
	{
		var builder = StringBuilderCache.Acquire();
		WriteLogPrefix(managedThreadId, dateTime, logLevel, builder);
		builder.Append(logLine);

		if (activity is not null)
		{
			// Accessing activity.Id here will cause the Id to be initialized
			// before the sampler runs in case where the activity is created using legacy way
			// i.e. new Activity("Operation name"). This will result in Id not reflecting the
			// correct sampling flags
			// https://github.com/dotnet/runtime/issues/61857

			var activityId = string.Concat("00-", activity.TraceId.ToHexString(), "-", activity.SpanId.ToHexString());
			builder.Append($" <{activityId}>");
		}

		var spin = new SpinWait();
		while (true)
		{
			if (_channel.Writer.TryWrite(StringBuilderCache.GetStringAndRelease(builder)))
				break;
			spin.SpinOnce();
		}
	}

	private static bool IsFileLoggingEnabled()
	{
		var enableFileLogging = true;

		var isEnabledValue = Environment.GetEnvironmentVariable(EnvironmentVariables.ElasticOtelFileLogging);

		if (!string.IsNullOrEmpty(isEnabledValue))
		{
			if (int.TryParse(isEnabledValue, out var intValue))
				enableFileLogging = intValue == 1;
			else if (bool.TryParse(isEnabledValue, out var boolValue))
				enableFileLogging = boolValue;
		}

		return enableFileLogging;
	}

	private static string GetElasticFolder =>
		RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Elastic" : "elastic";

	public void Dispose()
	{
		// We don't wait for the channel to be drained which is probably the correct choice.
		// Dispose should be a quick operation with no chance of exceptions.
		// We should document methods to wait for the WritingTask, before disposal, if that matters to the
		// consumer.

		_channel.Writer.TryComplete();
		_streamWriter.Dispose();
	}

	public async ValueTask DisposeAsync()
	{
		// We don't wait for the channel to be drained which is probably the correct choice.
		// Dispose should be a quick operation with no chance of exceptions.
		// We should document methods to await the WritingTask, before disposal, if that matters to the
		// consumer.

		_channel.Writer.TryComplete();
		await _streamWriter.DisposeAsync().ConfigureAwait(false);
	}
}
