// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics.Tracing;
using System.Text;
using System.Text.RegularExpressions;
using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.Diagnostics;

/// <summary>
/// Enables logging of OpenTelemetry-SDK event source events.
/// </summary>
internal sealed
#if NET8_0_OR_GREATER
	partial
#endif
	class LoggingEventListener(ILogger logger, CompositeElasticOpenTelemetryOptions options) : EventListener, IAsyncDisposable
{
	public const string OpenTelemetrySdkEventSourceNamePrefix = "OpenTelemetry-";

	private readonly ILogger _logger = logger;
	private readonly EventLevel _eventLevel = options.EventLogLevel;

	private const string TraceParentRegularExpressionString = "^\\d{2}-[a-f0-9]{32}-[a-f0-9]{16}-\\d{2}$";
#if NET8_0_OR_GREATER
	[GeneratedRegex(TraceParentRegularExpressionString)]
	private static partial Regex TraceParentRegex();
#else
	private static readonly Regex TraceParentRegexExpression = new(TraceParentRegularExpressionString);
	private static Regex TraceParentRegex() => TraceParentRegexExpression;
#endif

	public override void Dispose()
	{
		if (_logger is IDisposable d)
			d.Dispose();

		base.Dispose();
	}

	public ValueTask DisposeAsync() =>
		_logger is IAsyncDisposable d ? d.DisposeAsync() : default;

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

		var logLevel = GetLogLevel(eventData);

		if (!_logger.IsEnabled(logLevel))
			return;

		// This should generally be reasonably efficient but we can consider switching
		// to a rented array and Span<char> if required.
		var builder = StringBuilderCache.Acquire();

#if NETSTANDARD2_0 || NETFRAMEWORK
		var timestamp = DateTime.UtcNow; //best effort in absence of real event timestamp
		var osThreadId = 0L;
#else
		var timestamp = eventData.TimeStamp;
		var osThreadId = eventData.OSThreadId;
#endif

		var spanId = CreateLogMessage(eventData, builder, osThreadId);

		// TODO - We can only get the OS thread ID from the args - Do we send that instead??
		// As per this issue - https://github.com/dotnet/runtime/issues/13125 - OnEventWritten may be on a different thread
		// so we can't use the Environment.CurrentManagedThreadId value here.
		_logger.WriteLogLine(null, -1, timestamp, logLevel, StringBuilderCache.GetStringAndRelease(builder), spanId);

		static LogLevel GetLogLevel(EventWrittenEventArgs eventData) =>
			eventData.Level switch
			{
				EventLevel.Critical => LogLevel.Critical,
				EventLevel.Error => LogLevel.Error,
				EventLevel.Warning => LogLevel.Warning,
				EventLevel.Informational => LogLevel.Information,
				EventLevel.Verbose => LogLevel.Debug,
				EventLevel.LogAlways => LogLevel.Information,
				_ => LogLevel.None
			};

		static string? CreateLogMessage(EventWrittenEventArgs eventData, StringBuilder builder, long threadId)
		{
			string? spanId = null;

			if (eventData.EventSource.Name.StartsWith(OpenTelemetrySdkEventSourceNamePrefix) && eventData.Message is not null)
			{
				builder.Append($"OTEL-SDK: [{threadId}] ");

				if (eventData.Payload is null)
				{
					builder.Append(eventData.Message);
					return spanId;
				}

				try
				{
					var matchedActivityId = eventData.Payload.SingleOrDefault(p => p is string ps && TraceParentRegex().IsMatch(ps));

					if (matchedActivityId is string payloadString)
						spanId = payloadString[36..^3];

					var message = string.Format(eventData.Message, [.. eventData.Payload]);
					builder.Append(message);
					return spanId;
				}
				catch
				{
					for (var i = 0; i < eventData.Payload.Count; i++)
					{
						builder.Append(" | ");

						var payload = eventData.Payload[i];

						if (payload is not null)
#if NETFRAMEWORK
							// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
#endif
							builder.Append(payload.ToString() ?? "null");
						else
							builder.Append("null");
					}
				}
			}

			return spanId;
		}
	}
}
