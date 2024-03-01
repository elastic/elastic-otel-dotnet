// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections.ObjectModel;
using Elastic.OpenTelemetry.Diagnostics.Logging;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Elastic.OpenTelemetry.Tests;

public class TestLogger(ITestOutputHelper testOutputHelper) : ILogger
{
	private readonly List<string> _messages = new();
	public IReadOnlyCollection<string> Messages => _messages.AsReadOnly();

	public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopDisposable.Instance;

	public bool IsEnabled(LogLevel logLevel)
		=> true;

	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		var message = LogFormatter.Format(logLevel, eventId, state, exception, formatter);
		_messages.Add(message);
		testOutputHelper.WriteLine(message);
		if (exception != null)
			testOutputHelper.WriteLine(exception.ToString());

	}

	private class NoopDisposable : IDisposable
	{
		public static readonly NoopDisposable Instance = new();
		public void Dispose()
		{ }
	}
}

public class LoggingTests(ITestOutputHelper output)
{
    [Fact]
    public async Task ObserveLogging()
	{
		var logger = new TestLogger(output);
        const string activitySourceName = "TestSource";

        var activitySource = new ActivitySource(activitySourceName, "1.0.0");

        using var agent = new AgentBuilder(logger)
			.SkipOtlpExporter()
            .ConfigureTracer(tpb => tpb
                .ConfigureResource(rb => rb.AddService("Test", "1.0.0"))
                .AddSource(activitySourceName)
                .AddInMemoryExporter(new List<Activity>()))
            .Build();

        using (var activity = activitySource.StartActivity("DoingStuff", ActivityKind.Internal))
        {
            activity?.SetStatus(ActivityStatusCode.Ok);
        }

		//explicitly dispose agent so logging finishes too.
		await agent.DisposeAsync();

		//assert preamble information gets logged
		logger.Messages.Should().ContainMatch("*Elastic OpenTelemetry Distribution:*");

		// assert agent initialized confirmation and stack trace gets dumped.
		logger.Messages.Should().ContainMatch("*AgentBuilder initialized*");

		// very lenient format check
		logger.Messages.Should().ContainMatch("[*][*][*][Info]*");

	}
}
