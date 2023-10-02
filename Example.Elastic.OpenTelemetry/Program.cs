// See https://aka.ms/new-console-template for more information

using System.Diagnostics;
using Elastic.OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

Console.WriteLine("Hello, World!");

using var agent = Agent.Build(
    traceConfiguration: trace =>  trace.AddConsoleExporter(),
    metricConfiguration: metric => metric.AddConsoleExporter()
);

var activitySource = new ActivitySource(agent.Service.Name);

for (var i = 0; i < 2; i++)
{
    using var parent = activitySource.StartActivity("Parent");
    await Task.Delay(TimeSpan.FromMilliseconds(10));
    await StartChildSpans();
}


async Task StartChildSpans()
{
    using var child = activitySource.StartActivity("Child");
    await Task.Delay(TimeSpan.FromMilliseconds(10));

    // These have effectively ActivitySource = "", there is no way to include this without enabling ALL
    // activities on TracerBuilderProvider.AddSource("*")
    using var a = new Activity("Child2").Start();
    await Task.Delay(TimeSpan.FromMilliseconds(10));
}


