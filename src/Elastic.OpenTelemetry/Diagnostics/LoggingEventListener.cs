// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information
using System.Diagnostics.Tracing;
using System.Text;

namespace Elastic.OpenTelemetry.Diagnostics;

internal sealed class LoggingEventListener : EventListener, IAsyncDisposable
{
	public const string OpenTelemetrySdkEventSourceNamePrefix = "OpenTelemetry-";

	private readonly LogFileWriter _logFileWriter;
	private readonly EventLevel _eventLevel = EventLevel.Informational;

	public LoggingEventListener(LogFileWriter logFileWriter)
    {
		_logFileWriter = logFileWriter;

		var eventLevel = LogFileWriter.GetConfiguredLogLevel();

		_eventLevel = eventLevel switch
		{
			LogLevel.Trace => EventLevel.Verbose,
			LogLevel.Info => EventLevel.Informational,
			LogLevel.Warning => EventLevel.Warning,
			LogLevel.Error => EventLevel.Error,
			LogLevel.Critical => EventLevel.Critical,
			_ => EventLevel.Informational // fallback to info level
		};
	}

	public override void Dispose()
	{
		_logFileWriter.Dispose();
		base.Dispose();
	}

	public ValueTask DisposeAsync() => _logFileWriter.DisposeAsync();

	protected override void OnEventSourceCreated(EventSource eventSource)
	{
		if (eventSource.Name.StartsWith(OpenTelemetrySdkEventSourceNamePrefix, StringComparison.Ordinal))
			EnableEvents(eventSource, _eventLevel, EventKeywords.All);

		base.OnEventSourceCreated(eventSource);
	}

	protected override void OnEventWritten(EventWrittenEventArgs eventData)
	{
		if (!eventData.EventSource.Name.StartsWith(OpenTelemetrySdkEventSourceNamePrefix, StringComparison.Ordinal))
		{
			// Workaround for https://github.com/dotnet/runtime/issues/31927
			// EventCounters are published to all EventListeners, regardless of
			// which EventSource providers a listener is enabled for.
			return;
		}

		// This should generally be reasonably efficient but we can consider switching
		// to a rented array and Span<char> if required.
		var builder = StringBuilderCache.Acquire();

		CreateLogMessage(eventData, builder);

		try
		{
			// TODO - We can only get the OS thread ID from the args - Do we send that instead??
			// As per this issue - https://github.com/dotnet/runtime/issues/13125 - OnEventWritten may be on a different thread
			// so we can't use the Environment.CurrentManagedThreadId value here.
			_logFileWriter.WriteLogLine(null, -1, eventData.TimeStamp, GetLogLevel(eventData), StringBuilderCache.GetStringAndRelease(builder));
		}
		catch (Exception)
		{
			// TODO - We might want to block writing further events if we reach here as it's
			// likely a file access issue
		}

		static LogLevel GetLogLevel(EventWrittenEventArgs eventData) =>
			eventData.Level switch
			{
				EventLevel.Critical => LogLevel.Critical,
				EventLevel.Error => LogLevel.Error,
				EventLevel.Warning => LogLevel.Warning,
				EventLevel.Informational => LogLevel.Info,
				EventLevel.Verbose => LogLevel.Trace,
				EventLevel.LogAlways => LogLevel.Unknown,
				_ => LogLevel.Unknown
			};

		static void CreateLogMessage(EventWrittenEventArgs eventData, StringBuilder builder)
		{
			// Handle events from the OpenTelemetry SDK
			if (eventData.EventSource.Name.StartsWith(OpenTelemetrySdkEventSourceNamePrefix) && eventData.Message is not null)
			{
				LogMessageAndPayload(eventData, builder);
			}

			static void LogMessageAndPayload(EventWrittenEventArgs eventData, StringBuilder builder)
			{
				builder.Append($"OTEL-SDK: [{eventData.OSThreadId}] ");
				builder.Append(eventData.Message);

				if (eventData.Payload is not null)
				{
					for (var i = 0; i < eventData.Payload.Count; i++)
					{
						builder.Append(" - ");

						var payload = eventData.Payload[i];

						if (payload is not null)
						{
							builder.Append(payload.ToString() ?? "null");
						}
						else
						{
							builder.Append("null");
						}
					}
				}
			}
		}
	}
}
