// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using Example.Api;
using OpenTelemetry;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services
	.AddHttpClient() // Adds IHttpClientFactory
	.AddOpenTelemetry() // Adds the OpenTelemetry SDK
		.WithTracing(t => t.AddSource(Api.ActivitySourceName));

var app = builder.Build();

app.UseHttpsRedirection();

app.MapGet("/", (IHttpClientFactory httpClientFactory) => Api.HandleRoot(httpClientFactory));

app.Run();

namespace Example.Api
{
	internal static class Api
	{
		public static string ActivitySourceName = "CustomActivitySource";
		private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

		public static async Task<IResult> HandleRoot(IHttpClientFactory httpClientFactory)
		{
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
