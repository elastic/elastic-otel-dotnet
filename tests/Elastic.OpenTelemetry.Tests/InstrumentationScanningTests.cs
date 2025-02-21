// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using OpenTelemetry;
using Xunit.Abstractions;

namespace Elastic.OpenTelemetry.Tests;

public class InstrumentationScanningTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
	: IClassFixture<WebApplicationFactory<Program>>
{
	private readonly WebApplicationFactory<Program> _factory = factory;
	private readonly ITestOutputHelper _output = output;

	[Fact]
	public async Task MultipleCallsToAddAspNetCoreInstrumentation_DoNotDuplicateExportedSpans()
	{
		var exportedItems = new List<Activity>();

		var options = new ElasticOpenTelemetryOptions()
		{
			SkipOtlpExporter = true,
			AdditionalLogger = new TestLogger(_output)
		};

		var factory = _factory.WithWebHostBuilder(builder => builder
			.ConfigureTestServices(services => services.AddElasticOpenTelemetry(options)
			.WithTracing(tpb => tpb
				.ConfigureResource(rb => rb.AddService("Test", "1.0.0"))
				.AddAspNetCoreInstrumentation() // this is redundant, but we're testing that it being added twice doesn't introduce duplicated spans
				.AddInMemoryExporter(exportedItems)
			)));

		var client = factory.CreateClient();

		var response = await client.GetAsync("/");

		response.EnsureSuccessStatusCode();

		Assert.Equal("GET /", Assert.Single(exportedItems).DisplayName);
	}

	[Fact]
	public async Task MultipleCallsToAddHttpClientInstrumentation_DoNotDuplicateExportedSpans()
	{
		const string activitySourceName = nameof(MultipleCallsToAddHttpClientInstrumentation_DoNotDuplicateExportedSpans);
		var activitySource = new ActivitySource(activitySourceName, "1.0.0");

		var exportedItems = new List<Activity>();

		var host = Host.CreateDefaultBuilder();
		host.ConfigureServices(s =>
		{
			var options = new ElasticOpenTelemetryOptions()
			{
				SkipOtlpExporter = true,
				AdditionalLogger = new TestLogger(_output)
			};

			s.AddElasticOpenTelemetry(options)
				.WithTracing(tpb => tpb
					.ConfigureResource(rb => rb.AddService("Test", "1.0.0"))
					.AddHttpClientInstrumentation()
					.AddSource(activitySourceName)
					.AddInMemoryExporter(exportedItems)
				);
		});

		var ctx = new CancellationTokenRegistration();
		using (var app = host.Build())
		{
			_ = app.RunAsync(ctx.Token);

			using (var activity = activitySource.StartActivity(ActivityKind.Internal))
			{
				var client = new HttpClient();

				await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, "https://www.elastic.co"));

				activity?.SetStatus(ActivityStatusCode.Ok);
			}
				
			await ctx.DisposeAsync();
		}

		Assert.Single(exportedItems, a => a.DisplayName.Equals("HEAD", StringComparison.Ordinal));
	}

	[Fact]
	public async Task MultipleCallsToAddElasticsearchClientInstrumentation_DoNotDuplicateExportedSpans()
	{
		var exportedItems = new List<Activity>();

		var options = new ElasticOpenTelemetryOptions()
		{
			SkipOtlpExporter = true,
			AdditionalLogger = new TestLogger(_output)
		};

		var factory = _factory.WithWebHostBuilder(builder => builder
			.ConfigureTestServices(services => services.AddElasticOpenTelemetry(options)
			.WithTracing(tpb => tpb
				.ConfigureResource(rb => rb.AddService("Test", "1.0.0"))
				.AddElasticsearchClientInstrumentation() // this is redundant, but we're testing that it being added twice doesn't introduce duplicated spans
				.AddInMemoryExporter(exportedItems)
			)));

		var client = factory.CreateClient();

		var response = await client.GetAsync("/nest");

		response.EnsureSuccessStatusCode();

		Assert.Single(exportedItems, a => a.DisplayName.Equals("Elasticsearch HEAD", StringComparison.Ordinal));
	}
}
