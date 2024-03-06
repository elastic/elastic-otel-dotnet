// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information
using System.Diagnostics;
using Elastic.OpenTelemetry;
using Elastic.OpenTelemetry.Extensions;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Example.Elastic.OpenTelemetry;

internal static class Usage
{
	private const string ActivitySourceName = "CustomActivitySource";
	private static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");
	private static readonly HttpClient HttpClient = new();

	public static async Task BasicBuilderUsageAsync()
	{
		// NOTE: This sample assumes ENV VARs have been set to configure the Endpoint and Authorization header.

		// Build an agent by creating and using an agent builder, adding a single source (for traces and metrics) defined in this sample application.
		await using var agent = new AgentBuilder(ActivitySourceName).Build();

		// This example adds the application activity source and fully customises the resource
		await using var agent3 = new AgentBuilder(ActivitySourceName)
			.WithTracing(b => b.ConfigureResource(r => r.Clear().AddService("CustomServiceName", serviceVersion: "2.2.2")))
			.Build();

		await using var agent4 = new AgentBuilder()
			.WithTracing(t => t
				.ConfigureResource(rb => rb.AddService("TracerProviderBuilder", "3.3.3"))
				.AddRedisInstrumentation() // This can currently only be achieved using this overload or adding Elastic processors to the TPB (as below)
				.AddSource(ActivitySourceName)
				.AddConsoleExporter()
			)
			.WithTracing(tpb => tpb
				.ConfigureResource(rb => rb.AddService("TracerProviderBuilder", "3.3.3"))
				.AddRedisInstrumentation() // This can currently only be achieved using this overload or adding Elastic processors to the TPB (as below)
				.AddSource(ActivitySourceName)
				.AddConsoleExporter())
			.Build();

		//This is the most flexible approach for a consumer as they can include our processor(s) and exporter(s)
		using var tracerProvider = Sdk.CreateTracerProviderBuilder()
			.AddSource(ActivitySourceName)
			.ConfigureResource(resource =>
				resource.AddService(
				  serviceName: "OtelSdkApp",
				  serviceVersion: "1.0.0"))
			.AddConsoleExporter()
			.AddElasticProcessors()
			//.AddElasticOtlpExporter()
			.Build();

		await DoStuffAsync();

		static async Task DoStuffAsync()
		{
			using var activity = ActivitySource.StartActivity("DoingStuff", ActivityKind.Internal);
			activity?.SetTag("CustomTag", "TagValue");

			await Task.Delay(100);
			var response = await HttpClient.GetAsync("http://elastic.co");
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
