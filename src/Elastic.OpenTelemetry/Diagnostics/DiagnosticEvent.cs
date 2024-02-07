// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information
using System.Diagnostics;

namespace Elastic.OpenTelemetry.Diagnostics;

internal readonly struct DiagnosticEvent(Activity? activity = null) : IDiagnosticEvent
{
	public int ManagedThreadId { get; } = Environment.CurrentManagedThreadId;

	public DateTime DateTime { get; } = DateTime.UtcNow;

	public Activity? Activity { get; } = activity ?? Activity.Current;
}

internal readonly struct DiagnosticEvent<T>(T data, Activity? activity = null) : IDiagnosticEvent
{
	public T Data { get; init; } = data;

	public int ManagedThreadId { get; } = Environment.CurrentManagedThreadId;

	public DateTime DateTime { get; } = DateTime.UtcNow;

	public Activity? Activity { get; } = activity ?? Activity.Current;
}

