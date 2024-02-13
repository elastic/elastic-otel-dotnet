// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;
using System.Xml.Serialization;

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

		var builder = StringBuilderCache.Acquire();

		// Preamble and configuration entries are ALWAYS logged, regardless of the configured log level
		LogPreamble(process, builder, _streamWriter);
		LogConfiguration(builder, _streamWriter);

		StringBuilderCache.Release(builder);

		_streamWriter.AutoFlush = true; // Ensure we don't lose logs by not flushing to the file.

		static void LogPreamble(Process process, StringBuilder stringBuilder, StreamWriter streamWriter)
		{
			string[] preAmble = [
				$"Elastic OpenTelemetry Distribution: {Agent.InformationalVersion}",
				$"Process ID: {process.Id}",
				$"Process name: {process.ProcessName}",
				$"Process path: {Environment.ProcessPath}",
				$"Process started: {process.StartTime.ToUniversalTime():yyyy-MM-dd HH:mm:ss.fff}",
				$"Machine name: {Environment.MachineName}",
				$"Process username: {Environment.UserName}",
				$"User domain name: {Environment.UserDomainName}",
				$"Command line: {Environment.CommandLine}",
				$"Command current directory: {Environment.CurrentDirectory}",
				$"Processor count: {Environment.ProcessorCount}",
				$"OS version: {Environment.OSVersion}",
				$"CLR version: {Environment.Version}",
			];

			foreach (var item in preAmble)
			{
				WriteLogPrefix(LogLevel.Info, stringBuilder);
				stringBuilder.Append(item);
				streamWriter.WriteLine(stringBuilder.ToString());
				stringBuilder.Clear();
			}

			streamWriter.Flush();
		}

		static void LogConfiguration(StringBuilder stringBuilder, StreamWriter streamWriter)
		{
			string[] environmentVariables = [
				EnvironmentVariables.ElasticOtelFileLogging,
				EnvironmentVariables.ElasticOtelLogDirectoryEnvironmentVariable,
				EnvironmentVariables.ElasticOtelLogLevelEnvironmentVariable
			];

			foreach (var variable in environmentVariables)
			{
				var envVarValue = Environment.GetEnvironmentVariable(variable);

				WriteLogPrefix(LogLevel.Info, stringBuilder);

				if (!string.IsNullOrEmpty(envVarValue))
				{
					stringBuilder.Append($"Environment variable '{variable}' = '{envVarValue}'.");
					streamWriter.WriteLine(stringBuilder.ToString());
				}
				else
				{
					stringBuilder.Append($"Environment variable '{variable}' is not configured.");
					streamWriter.WriteLine(stringBuilder.ToString());
				}

				stringBuilder.Clear();
			}

			streamWriter.Flush();
		}
	}

	private static readonly LogLevel ConfiguredLogLevel = GetConfiguredLogLevel();

	public static LogLevel GetConfiguredLogLevel()
	{
		var logLevel = LogLevel.Info;

		var logLevelEnvironmentVariable = Environment.GetEnvironmentVariable(EnvironmentVariables.ElasticOtelLogLevelEnvironmentVariable);

		if (!string.IsNullOrEmpty(logLevelEnvironmentVariable))
		{
			if (logLevelEnvironmentVariable.Equals(DiagnosticErrorLevels.Trace, StringComparison.OrdinalIgnoreCase))
				logLevel = LogLevel.Trace;

			else if (logLevelEnvironmentVariable.Equals(DiagnosticErrorLevels.Info, StringComparison.OrdinalIgnoreCase))
				logLevel = LogLevel.Info;

			else if (logLevelEnvironmentVariable.Equals(DiagnosticErrorLevels.Warning, StringComparison.OrdinalIgnoreCase))
				logLevel = LogLevel.Warning;

			else if (logLevelEnvironmentVariable.Equals(DiagnosticErrorLevels.Error, StringComparison.OrdinalIgnoreCase))
				logLevel = LogLevel.Error;

			else if (logLevelEnvironmentVariable.Equals(DiagnosticErrorLevels.Critical, StringComparison.OrdinalIgnoreCase))
				logLevel = LogLevel.Critical;
		}

		return logLevel;
	}

	private static void WriteLogPrefix(LogLevel logLevel, StringBuilder builder) =>
		WriteLogPrefix(Environment.CurrentManagedThreadId, DateTime.UtcNow, logLevel, builder);

	private const string EmptySpanId = "------";

	private static void WriteLogPrefix(int managedThreadId, DateTime dateTime, LogLevel level, StringBuilder builder, string spanId = "")
	{
		const int maxLength = 5;

		if (string.IsNullOrEmpty(spanId))
			spanId = EmptySpanId;

		var threadId = new string('-', maxLength);

		if (managedThreadId > 0)
		{
			var digits = (int)Math.Floor(Math.Log10(managedThreadId) + 1);

			if (digits < 5)
			{
				Span<char> buffer = stackalloc char[maxLength];
				for (var i = 0; i < maxLength - digits; i++)
				{
					buffer[i] = '0';
				}
				managedThreadId.TryFormat(buffer[(maxLength - digits)..], out _);
				threadId = buffer.ToString();
			}
			else
			{
				threadId = managedThreadId.ToString();
			}
		}

		builder.Append('[')
			.Append(dateTime.ToString("yyyy-MM-dd HH:mm:ss.fff"))
			.Append("][")
			.Append(threadId)
			.Append("][")
			.Append(spanId[..6])
			.Append("][")
			.Append(level.AsString())
			.Append(']');

		var length = builder.Length;
		var padding = 52 - length;

		for (var i = 0; i < padding; i++)
		{
			builder.Append(' ');
		}
	}

	public static LogFileWriter Instance { get; } = new();

	public string LogFilePath { get; }

	public Task WritingTask { get; }

	public void WriteCriticalLogLine(DiagnosticEvent diagnosticEvent, string message) =>
		WriteLogLine(diagnosticEvent, LogLevel.Critical, message);

	public void WriteErrorLogLine(DiagnosticEvent diagnosticEvent, string message) =>
		WriteLogLine(diagnosticEvent, LogLevel.Error, message);

	public void WriteWarningLogLine(DiagnosticEvent diagnosticEvent, string message) =>
		WriteLogLine(diagnosticEvent, LogLevel.Warning, message);

	public void WriteInfoLogLine(DiagnosticEvent diagnosticEvent, string message) =>
		WriteLogLine(diagnosticEvent, LogLevel.Info, message);

	public void WriteTraceLogLine(DiagnosticEvent diagnosticEvent, string message) =>
		WriteLogLine(diagnosticEvent, LogLevel.Trace, message);

	public void WriteLogLine(DiagnosticEvent diagnosticEvent, LogLevel logLevel, string message) =>
		WriteLogLine(diagnosticEvent.Activity, diagnosticEvent.ManagedThreadId, diagnosticEvent.DateTime, logLevel, message);

	public void WriteLogLine(Activity? activity, int managedThreadId, DateTime dateTime, LogLevel logLevel, string logLine) =>
		WriteLogLine(activity, managedThreadId, dateTime, logLevel, logLine, null);

	public void WriteLogLine(Activity? activity, int managedThreadId, DateTime dateTime, LogLevel logLevel, string logLine, string? spanId)
	{
		// We skip logging for any log level higher (numerically) than the configured log level
		if (logLevel > ConfiguredLogLevel)
			return;

		var builder = StringBuilderCache.Acquire();
		WriteLogPrefix(managedThreadId, dateTime, logLevel, builder, spanId ?? activity?.SpanId.ToHexString() ?? string.Empty);
		builder.Append(logLine);

		if (activity is not null)
		{
			// Accessing activity.Id here will cause the Id to be initialized
			// before the sampler runs in case where the activity is created using legacy way
			// i.e. new Activity("Operation name"). This will result in Id not reflecting the
			// correct sampling flags
			// https://github.com/dotnet/runtime/issues/61857

			var activityId = $"00-{activity.TraceId.ToHexString()}-{activity.SpanId.ToHexString()}-{(activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded) ? "01" : "00")}";
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
