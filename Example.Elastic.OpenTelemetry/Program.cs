using System.Diagnostics;
using Elastic.OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

using var agent = Agent.Build(
    traceConfiguration: trace => trace.AddConsoleExporter(),
    metricConfiguration: metric => metric.AddConsoleExporter()
);
//agent && Agent.Current now pointing to the same instance;
var activitySource = Agent.Current.ActivitySource;

for (var i = 0; i < 2; i++)
{
    using var parent = activitySource.StartActivity("Parent");
    await Task.Delay(TimeSpan.FromSeconds(0.25));
    await StartChildSpansForCompressionAsync();
    await Task.Delay(TimeSpan.FromSeconds(0.25));
    await StartChildSpansAsync();
    await Task.Delay(TimeSpan.FromSeconds(0.25));
    using var _ = activitySource.StartActivity("ChildTwo");
    await Task.Delay(TimeSpan.FromSeconds(0.25));
}

Console.WriteLine("DONE");

async Task StartChildSpansForCompressionAsync()
{
    for (var i = 0; i < 10; i++)
    {
        using var child = activitySource.StartActivity("ChildSpanCompression");
        await Task.Delay(TimeSpan.FromSeconds(0.25));
    }
}

async Task StartChildSpansAsync()
{
    using var child = activitySource.StartActivity("Child");
    await Task.Delay(TimeSpan.FromMilliseconds(10));

    // These have effectively ActivitySource = "", there is no way to include this without enabling ALL
    // activities on TracerBuilderProvider.AddSource("*")
    using var a = new Activity("Child2").Start();
    await Task.Delay(TimeSpan.FromMilliseconds(10));
}