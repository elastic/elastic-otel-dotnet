// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Diagnostics;
using Xunit.Abstractions;

namespace Elastic.OpenTelemetry.Tests;

public class TestLogger(ITestOutputHelper testOutputHelper) : ILogger
{
	private readonly List<string> _messages = [];
	private readonly ITestOutputHelper _testOutputHelper = testOutputHelper;

	public IReadOnlyCollection<string> Messages => _messages.AsReadOnly();

	public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopDisposable.Instance;

	public bool IsEnabled(LogLevel logLevel) => true;

	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		var message = LogFormatter.Format(logLevel, eventId, state, exception, formatter);
		_messages.Add(message);
		_testOutputHelper.WriteLine(message);
		if (exception != null)
			_testOutputHelper.WriteLine(exception.ToString());
	}

	private class NoopDisposable : IDisposable
	{
		public static readonly NoopDisposable Instance = new();

		public void Dispose() { }
	}
}
