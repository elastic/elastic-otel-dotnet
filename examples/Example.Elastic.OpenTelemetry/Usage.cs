using System.Diagnostics;
using Elastic.OpenTelemetry;

namespace Example.Elastic.OpenTelemetry;

internal static class Usage
{
    private const string ActivitySourceName = "CustomActivitySource";
    private static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");
    private static readonly HttpClient HttpClient = new();

    public static async Task BasicBuilderUsageAsync()
    {
        // NOTE: This sample assumes ENV VARs have been set to configure the Endpoint and Authorization header.

        // Build an agent by creating and using an agent builder, adding a single source defined in this sample application.
        using var agent = new AgentBuilder().Tracer.AddSource(ActivitySourceName).Build();

        //using var agent = new AgentBuilder()
        //    .Tracer.AddSource(ActivitySourceName)
        //    .Tracer.ConfigureResourceBuilder(b => b.Clear().AddService("CustomServiceName", serviceVersion: "2.2.2"))
        //    .Build();

        await DoStuffAsync();

        static async Task DoStuffAsync()
        {
            using var activity = ActivitySource.StartActivity("DoingStuff", ActivityKind.Internal);
            activity?.SetTag("CustomTag", "TagValue");

            await Task.Delay(100);
            var response = await HttpClient.GetAsync("https://elastic.co");
            await Task.Delay(50);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
                activity?.SetStatus(ActivityStatusCode.Ok);
            else
                activity?.SetStatus(ActivityStatusCode.Error);
        }
    }

    //public static async Task ComplexUsageAsync()
    //{
    //    using var agent = Agent.Build(
    //        traceConfiguration: trace => trace.AddConsoleExporter(),
    //        metricConfiguration: metric => metric.AddConsoleExporter()
    //    );
    //    //agent && Agent.Current now pointing to the same instance;
    //    var activitySource = Agent.Current.ActivitySource;

    //    for (var i = 0; i < 2; i++)
    //    {
    //        using var parent = activitySource.StartActivity("Parent");
    //        await Task.Delay(TimeSpan.FromSeconds(0.25));
    //        await StartChildSpansForCompressionAsync();
    //        await Task.Delay(TimeSpan.FromSeconds(0.25));
    //        await StartChildSpansAsync();
    //        await Task.Delay(TimeSpan.FromSeconds(0.25));
    //        using var _ = activitySource.StartActivity("ChildTwo");
    //        await Task.Delay(TimeSpan.FromSeconds(0.25));
    //    }

    //    Console.WriteLine("DONE");

    //    async Task StartChildSpansForCompressionAsync()
    //    {
    //        for (var i = 0; i < 10; i++)
    //        {
    //            using var child = activitySource.StartActivity("ChildSpanCompression");
    //            await Task.Delay(TimeSpan.FromSeconds(0.25));
    //        }
    //    }

    //    async Task StartChildSpansAsync()
    //    {
    //        using var child = activitySource.StartActivity("Child");
    //        await Task.Delay(TimeSpan.FromMilliseconds(10));

    //        // These have effectively ActivitySource = "", there is no way to include this without enabling ALL
    //        // activities on TracerBuilderProvider.AddSource("*")
    //        using var a = new Activity("Child2").Start();
    //        await Task.Delay(TimeSpan.FromMilliseconds(10));
    //    }
    //}
}
