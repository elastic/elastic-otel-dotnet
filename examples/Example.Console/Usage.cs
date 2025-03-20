// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry;
using Microsoft.Extensions.Logging;
using OpenTelemetry;

namespace Example.Console;

internal static class Usage
{
	public static void BasicBuilderUsage()
	{
		// NOTE: This sample assumes ENV VARs have been set to configure the Endpoint and Authorization header.

		//// Build an instrumentation session by creating an ElasticOpenTelemetryBuilder.
		//// The application will be instrumented until the session is disposed.
		//await using var session = new ElasticOpenTelemetryBuilder()
		//	.WithTracing(b => b.AddSource(ActivitySourceName))
		//	.Build();

		//await using var session2 = new ElasticOpenTelemetryBuilder().Build();

		//// This example adds the application activity source and fully customises the resource
		//await using var session3 = new ElasticOpenTelemetryBuilder()
		//	.WithTracing(b => b
		//		.AddSource(ActivitySourceName)
		//		.ConfigureResource(r => r.Clear().AddService("CustomServiceName", serviceVersion: "2.2.2")))
		//	.Build();

		//await using var session4 = new ElasticOpenTelemetryBuilder()
		//	.WithTracing(t => t
		//		.ConfigureResource(rb => rb.AddService("TracerProviderBuilder", "3.3.3"))
		//		.AddRedisInstrumentation() // This can currently only be achieved using this overload or adding Elastic processors to the TPB (as below)
		//		.AddSource(ActivitySourceName)
		//		.AddConsoleExporter()
		//	)
		//	.WithTracing(tpb => tpb
		//		.ConfigureResource(rb => rb.AddService("TracerProviderBuilder", "3.3.3"))
		//		.AddRedisInstrumentation() // This can currently only be achieved using this overload or adding Elastic processors to the TPB (as below)
		//		.AddSource(ActivitySourceName)
		//		.AddConsoleExporter())
		//	.Build();

		using var loggerFactory = LoggerFactory.Create(static builder =>
		{
			builder
			   .AddFilter("Elastic.OpenTelemetry", LogLevel.Debug)
			   .AddConsole();
		});

		var options = new ElasticOpenTelemetryOptions
		{
			AdditionalLoggerFactory = loggerFactory
		};

		using var sdk = OpenTelemetrySdk.Create(builder => builder
		   .WithElasticDefaults(options));

		//This is the most flexible approach for a consumer as they can include our processor(s)
		//using var tracerProvider = Sdk.CreateTracerProviderBuilder()
		//	.AddSource(ActivitySourceName)
		//	.ConfigureResource(resource =>
		//		resource.AddService(
		//		  serviceName: "OtelSdkApp",
		//		  serviceVersion: "1.0.0"))
		//	.AddConsoleExporter()
		//	.AddElasticProcessors()
		////	.Build();

		//await DoStuffAsync();

		//static async Task DoStuffAsync()
		//{
		//	using var activity = ActivitySource.StartActivity("DoingStuff", ActivityKind.Internal);
		//	activity?.SetTag("CustomTag", "TagValue");

		//	await Task.Delay(100);
		//	var response = await HttpClient.GetAsync("http://elastic.co");
		//	await Task.Delay(50);

		//	if (response.StatusCode == System.Net.HttpStatusCode.OK)
		//		activity?.SetStatus(ActivityStatusCode.Ok);
		//	else
		//		activity?.SetStatus(ActivityStatusCode.Error);
		//}
	}
}
