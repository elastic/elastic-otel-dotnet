// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.Tests.Diagnostics;

/// <summary>
/// An <see cref="ILogger"/> that captures the <see cref="EventId"/> from each log call
/// without writing anything. Used by the EventId snapshot test to verify that
/// <c>[LoggerMessage]</c>-generated code emits the expected EventIds at runtime.
/// </summary>
internal sealed class EventIdCapturingLogger : ILogger
{
	public List<(int Id, string? Name)> CapturedEventIds { get; } = [];

	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
		Exception? exception, Func<TState, Exception?, string> formatter)
	{
		if (eventId.Id != 0) // skip default/unset
			CapturedEventIds.Add((eventId.Id, eventId.Name));
	}

	public bool IsEnabled(LogLevel logLevel) => true;

	public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
}
