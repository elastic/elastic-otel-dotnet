// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.Diagnostics.Logging;

internal static class LogFormatter
{
	public static string Format<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		var managedThreadId = Environment.CurrentManagedThreadId;
		var dateTime = DateTime.UtcNow;
		string? spanId = null;
		var activity = Activity.Current;
		switch (state)
		{
			case LogState s:
				managedThreadId = s.ManagedThreadId;
				dateTime = s.DateTime;
				activity = s.Activity;
				spanId = s.SpanId;
				break;
		}

		var builder = StringBuilderCache.Acquire();

		WriteLogPrefix(managedThreadId, dateTime, logLevel, builder, spanId ?? activity?.SpanId.ToHexString() ?? string.Empty);
		var message = formatter(state, exception);
		builder.Append(message);

		//todo force Exception to be written as error

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
		var fullLogLine = StringBuilderCache.GetStringAndRelease(builder);
		return fullLogLine;
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
					buffer[i] = '0';
				managedThreadId.TryFormat(buffer[(maxLength - digits)..], out _);
				threadId = buffer.ToString();
			}
			else
				threadId = managedThreadId.ToString();
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
			builder.Append(' ');
	}

}
