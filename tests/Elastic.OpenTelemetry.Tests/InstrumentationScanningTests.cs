// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using OpenTelemetry;
using Xunit.Abstractions;

namespace Elastic.OpenTelemetry.Tests;

#if NET8_0
public partial class InstrumentationScanningTests(WebApplicationFactory<WebApiDotNet8.ProgramV8> factory, ITestOutputHelper output)
	: IClassFixture<WebApplicationFactory<WebApiDotNet8.ProgramV8>>
{
	private readonly WebApplicationFactory<WebApiDotNet8.ProgramV8> _factory = factory;
#else
public partial class InstrumentationScanningTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
	: IClassFixture<WebApplicationFactory<Program>>
{
	private readonly WebApplicationFactory<Program> _factory = factory;
#endif
	private readonly ITestOutputHelper _output = output;

#if NET8_0
	[GeneratedRegex(@"^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}\]\[\d{6}\]\[-*\]\[Debug\]\s+Added contrib instrumentation 'HTTP' to TracerProviderBuilder*")]
	private static partial Regex HttpTracerProviderBuilderRegex();

	[GeneratedRegex(@"^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}\]\[\d{6}\]\[-*\]\[Debug\]\s+Added contrib instrumentation 'HTTP' to MeterProviderBuilder*")]
	private static partial Regex HttpMeterProviderBuilderRegex();
#elif NET9_0
	[GeneratedRegex(@"^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}\]\[\d{6}\]\[-*\]\[Debug\]\s+Added 'System.Net.Http' to TracerProviderBuilder.*")]
	private static partial Regex HttpTracerProviderBuilderRegex();

	[GeneratedRegex(@"^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}\]\[\d{6}\]\[-*\]\[Debug\]\s+Added 'System.Net.Http' meter to MeterProviderBuilder.*")]
	private static partial Regex HttpMeterProviderBuilderRegex();
#endif

	[Fact]
	public async Task InstrumentationAssemblyScanning_AddsAspNetCoreInstrumentation()
	{
		var exportedItems = new List<Activity>();
		var logger = new TestLogger(_output);
		var options = new ElasticOpenTelemetryOptions()
		{
			SkipOtlpExporter = true,
			AdditionalLogger = logger
		};

		using var factory = _factory.WithWebHostBuilder(builder => builder
			.ConfigureTestServices(services => services
			.AddElasticOpenTelemetry(options)
				.WithTracing(tpb => tpb.AddInMemoryExporter(exportedItems))));

		using var client = factory.CreateClient();

		using var response = await client.GetAsync("/");

		response.EnsureSuccessStatusCode();

		Assert.Equal("GET /", Assert.Single(exportedItems).DisplayName);
	}

	[Fact]
	public async Task InstrumentationAssemblyScanning_AddsHttpInstrumentation()
	{
		// NOTE: When this runs on NET8, we expect the contrib library to be used.
		// On NET9, the library dependency is not included with Elastic.OpenTelemetry,
		// so we expect the native instrumentation to be used.

		var exportedItems = new List<Activity>();
		var logger = new TestLogger(_output);
		var options = new ElasticOpenTelemetryOptions()
		{
			SkipOtlpExporter = true,
			AdditionalLogger = logger
		};

		using var factory = _factory.WithWebHostBuilder(builder => builder
			.ConfigureTestServices(services => services
			.AddElasticOpenTelemetry(options)
				.WithTracing(tpb => tpb.AddInMemoryExporter(exportedItems))));

		using var client = factory.CreateClient();

		using var response = await client.GetAsync("/http");

		response.EnsureSuccessStatusCode();

		Assert.Equal(2, exportedItems.Count); // One for ASP.NET Core and one for HTTP

		var activity = Assert.Single(exportedItems, a => a.DisplayName.Equals("GET", StringComparison.Ordinal));

		var urlFull = Assert.Single(activity.TagObjects, a => a.Key.Equals("url.full", StringComparison.Ordinal));
		Assert.Equal("https://example.com/", (string?)urlFull.Value);

		var foundExpectedHttpTracerInstrumentationMessage = false;
		var foundExpectedHttpMeterInstrumentationMessage = false;

		foreach (var message in logger.Messages)
		{
			if (HttpTracerProviderBuilderRegex().IsMatch(message))
				foundExpectedHttpTracerInstrumentationMessage = true;

			if (HttpMeterProviderBuilderRegex().IsMatch(message))
				foundExpectedHttpMeterInstrumentationMessage = true;
		}

		Assert.True(foundExpectedHttpTracerInstrumentationMessage, "Expected to find HTTP tracer instrumentation message in logs.");
		Assert.True(foundExpectedHttpMeterInstrumentationMessage, "Expected to find HTTP meter instrumentation message in logs.");
	}
}
