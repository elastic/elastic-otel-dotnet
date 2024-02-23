// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Elastic.OpenTelemetry.Diagnostics;

internal class DiagnosticEvent(Activity? activity = null, ILogger? logger = null)
{
	public int ManagedThreadId { get; } = Environment.CurrentManagedThreadId;

	public DateTime DateTime { get; } = DateTime.UtcNow;

	public Activity? Activity { get; } = activity ?? Activity.Current;

	public ILogger Logger { get; } = logger ?? NullLogger.Instance;
}

internal class DiagnosticEvent<T>(T data, Activity? activity = null, ILogger? logger = null) : DiagnosticEvent(activity, logger)
{
	public T Data { get; init; } = data;
}

