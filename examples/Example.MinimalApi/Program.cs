// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using Example.MinimalApi;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

var options = new Elastic.OpenTelemetry.ElasticOpenTelemetryOptions()
{
	// You can customize options here if needed
};

//// Force small batch size and no delay for quicker exports in this example
//builder.Services.Configure<OtlpExporterOptions>(o =>
//{
//	o.BatchExportProcessorOptions.MaxExportBatchSize = 1;
//	o.BatchExportProcessorOptions.ScheduledDelayMilliseconds = 1;
//});

builder.AddElasticOpenTelemetry(options, otelBuilder => otelBuilder
	.WithTracing(t => t
		.AddSource(Api.ActivitySourceName)
		.AddProcessor(new CustomProcessor())));

builder.AddServiceDefaults();

var app = builder.Build();

app.UseHttpsRedirection();

app.MapGet("/", (IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory) =>
	Api.HandleRoot(httpClientFactory, loggerFactory));

app.Run();

namespace Example.MinimalApi
{
	internal static class Api
	{
		public static string ActivitySourceName = "CustomActivitySource";
		private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

		public static async Task<IResult> HandleRoot(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory)
		{
			var logger = loggerFactory.CreateLogger("Example.Api");

			logger.LogInformation("Doing stuff");

			using var client = httpClientFactory.CreateClient();

			using var activity = ActivitySource.StartActivity("DoingStuff", ActivityKind.Internal);
			activity?.SetTag("custom-tag", "TagValue");

			await Task.Delay(100);
			var response = await client.GetAsync("http://elastic.co"); // using this URL will require 2 redirects
			await Task.Delay(50);

			if (response.StatusCode == System.Net.HttpStatusCode.OK)
			{
				activity?.SetStatus(ActivityStatusCode.Ok);
				return Results.Ok();
			}

			activity?.SetStatus(ActivityStatusCode.Error);
			return Results.StatusCode(500);
		}
	}
}
