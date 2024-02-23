// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Diagnostics;
using Elastic.OpenTelemetry.Diagnostics.Logging;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Elastic.OpenTelemetry.Tests;

public class TestLogger(ITestOutputHelper testOutputHelper) : ILogger
{
	public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopDisposable.Instance;

	public bool IsEnabled(LogLevel logLevel)
		=> true;

	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		var message = LogFormatter.Format(logLevel, eventId, state, exception, formatter);
		testOutputHelper.WriteLine(message);
		if (exception != null)
			testOutputHelper.WriteLine(exception.ToString());
	}

	private class NoopDisposable : IDisposable
	{
		public static readonly NoopDisposable Instance = new NoopDisposable();
		public void Dispose()
		{ }
	}
}

public class LoggingTests(ITestOutputHelper output)
{
    [Fact]
    public void ObserveLogging()
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

    }
}
