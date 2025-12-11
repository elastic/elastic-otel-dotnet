// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics.Tracing;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Core;
using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.Diagnostics;

/// <summary>
/// Enables logging of OpenTelemetry-SDK event source events.
/// </summary>
internal sealed
#if NET8_0_OR_GREATER
	partial
#endif
	class LoggingEventListener : EventListener, ICentralConfigurationSubscriber, IAsyncDisposable
{
	public const string OpenTelemetryEventSourceNamePrefix = "OpenTelemetry-";

	private const string TraceParentRegularExpressionString = "^\\d{2}-[a-f0-9]{32}-[a-f0-9]{16}-\\d{2}$";
#if NET8_0_OR_GREATER
	[GeneratedRegex(TraceParentRegularExpressionString)]
	private static partial Regex TraceParentRegex();
#else
	private static readonly Regex TraceParentRegexExpression = new(TraceParentRegularExpressionString);
	private static Regex TraceParentRegex() => TraceParentRegexExpression;
#endif

	private readonly CompositeLogger _logger;
	private readonly List<EventSource>? _eventSourcesBeforeConstructor = [];
	private readonly List<EventSource> _subscribedEventSources = [];
	private readonly Lock _lock = new();

	private EventLevel _eventLevel = EventLevel.Informational;

	public LoggingEventListener(CompositeLogger logger, CompositeElasticOpenTelemetryOptions options)
	{
		if (BootstrapLogger.IsEnabled)
		{
			BootstrapLogger.LogWithStackTrace($"{nameof(LoggingEventListener)}: Instance '{InstanceId}' created via ctor." +
				$"{Environment.NewLine}    Invoked with `{nameof(CompositeLogger)}` instance '{logger.InstanceId}'." +
				$"{Environment.NewLine}    Invoked with `{nameof(CompositeElasticOpenTelemetryOptions)}` instance '{options.InstanceId}'.");

			BootstrapLogger.Log($"{nameof(LoggingEventListener)}: {nameof(CompositeElasticOpenTelemetryOptions)}.{nameof(CompositeElasticOpenTelemetryOptions.EventLogLevel)} = '{options.EventLogLevel}'");
		}

		_logger = logger;
		_eventLevel = LogLevelToEventLevel(options.LogLevel);

		_logger.LogDebug("LoggingEventListener event level set to: `{EventLevel}`", _eventLevel.ToString());

		List<EventSource>? eventSources;

		using (_lock.EnterScope())
		{
			eventSources = _eventSourcesBeforeConstructor;
			_eventSourcesBeforeConstructor = null;
		}

		if (eventSources is not null)
		{
			foreach (var eventSource in eventSources)
			{
				using (_lock.EnterScope())
				{
					_subscribedEventSources.Add(eventSource);
				}

				EnableEvents(eventSource, _eventLevel, EventKeywords.All);

				_logger.LogDebug("LoggingEventListener subscribed to '{EventSourceName}' at level '{EventLevel}'", eventSource.Name, _eventLevel);
			}
		}
	}

	private static EventLevel LogLevelToEventLevel(LogLevel? eventLogLevel) =>
		eventLogLevel switch
		{
			LogLevel.Trace or LogLevel.Debug => EventLevel.LogAlways,
			LogLevel.Information => EventLevel.Informational,
			LogLevel.Warning => EventLevel.Warning,
			LogLevel.Error => EventLevel.Error,
			LogLevel.Critical => EventLevel.Critical,
			_ => EventLevel.Informational // fallback to info level
		};

	internal Guid InstanceId { get; } = Guid.NewGuid();

	public void OnConfiguration(RemoteConfiguration remoteConfiguration)
	{
		var newEventLevel = LogLevelToEventLevel(remoteConfiguration.LogLevel);

		using (_lock.EnterScope())
		{
			_eventLevel = newEventLevel;

			foreach (var eventSource in _subscribedEventSources)
			{
				EnableEvents(eventSource, _eventLevel, EventKeywords.All);

				_logger.LogDebug("LoggingEventListener updated '{EventSourceName}' subscription to level '{EventLevel}'", eventSource.Name, _eventLevel);
			}
		}
	}

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
		// When instantiating an EventListener, the callbacks to OnEventSourceCreated and OnEventWritten can happen before the constructor has completed.
		// Take care when you initialize instance members used in those callbacks.
		// See https://learn.microsoft.com/dotnet/api/system.diagnostics.tracing.eventlistener
		if (eventSource.Name.StartsWith(OpenTelemetryEventSourceNamePrefix, StringComparison.OrdinalIgnoreCase))
		{
			if (_eventSourcesBeforeConstructor is not null)
			{
				using (_lock.EnterScope())
				{
					if (_eventSourcesBeforeConstructor is not null)
					{
						_eventSourcesBeforeConstructor.Add(eventSource);
						return;
					}
				}
			}

			using (_lock.EnterScope())
			{
				_subscribedEventSources.Add(eventSource);
			}

			EnableEvents(eventSource, _eventLevel, EventKeywords.All);

			_logger.LogDebug("LoggingEventListener subscribed to '{EventSourceName}' at level '{EventLevel}'", eventSource.Name, _eventLevel);
		}

		base.OnEventSourceCreated(eventSource);
	}

	protected override void OnEventWritten(EventWrittenEventArgs eventData)
	{
		if (!eventData.EventSource.Name.StartsWith(OpenTelemetryEventSourceNamePrefix, StringComparison.OrdinalIgnoreCase))
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

			if (eventData.EventSource.Name.StartsWith(OpenTelemetryEventSourceNamePrefix) && eventData.Message is not null)
			{
				builder.Append($"OTEL-SDK ({eventData.EventSource.Name}): [{threadId}] ");

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
