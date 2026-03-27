// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.OpenTelemetry.Tests.Diagnostics;

/// <summary>
/// Thread-safe logger that counts how many times <see cref="Log{TState}"/> is called.
/// Shared across CompositeLogger test classes.
/// </summary>
internal sealed class CountingLogger : ILogger
{
	private int _count;

	public int Count => Volatile.Read(ref _count);

	public IDisposable BeginScope<TState>(TState state) where TState : notnull =>
		NullScope.Instance;

	public bool IsEnabled(LogLevel logLevel) => true;

	public void Log<TState>(
		LogLevel logLevel,
		EventId eventId,
		TState state,
		Exception? exception,
		Func<TState, Exception?, string> formatter) =>
		Interlocked.Increment(ref _count);

	private sealed class NullScope : IDisposable
	{
		public static readonly NullScope Instance = new();
		public void Dispose() { }
	}
}
