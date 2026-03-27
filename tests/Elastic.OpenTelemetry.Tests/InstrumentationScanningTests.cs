// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using OpenTelemetry;
using Xunit.Abstractions;

namespace Elastic.OpenTelemetry.Tests;

public partial class InstrumentationScanningTests(ITestOutputHelper output)
{
	private readonly ITestOutputHelper _output = output;

#if NET8_0
	[GeneratedRegex(@"^\[\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z\]\[\d{6}\]\[-*\]\[Debug\]\s+Added contrib instrumentation 'HTTP' to TracerProviderBuilder*")]
	private static partial Regex HttpTracerProviderBuilderRegex();

	[GeneratedRegex(@"^\[\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z\]\[\d{6}\]\[-*\]\[Debug\]\s+Added contrib instrumentation 'HTTP' to MeterProviderBuilder*")]
	private static partial Regex HttpMeterProviderBuilderRegex();
#elif NET9_0_OR_GREATER
	[GeneratedRegex(@"^\[\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z\]\[\d{6}\]\[-*\]\[Debug\]\s+Added 'System.Net.Http' to TracerProviderBuilder.*")]
	private static partial Regex HttpTracerProviderBuilderRegex();

	[GeneratedRegex(@"^\[\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z\]\[\d{6}\]\[-*\]\[Debug\]\s+Added 'System.Net.Http' meter to MeterProviderBuilder.*")]
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

		// Explicitly not using a shared fixture for this test to avoid potential cross-test pollution
		// of the exported items collection, which would make assertions more difficult to write and maintain.
#if NET8_0
		await using var factory = new WebApplicationFactory<WebApiDotNet8.ProgramV8>()
#elif NET9_0
		await using var factory = new WebApplicationFactory<WebApiDotNet9.ProgramV9>()
#else
		await using var factory = new WebApplicationFactory<Program>()
#endif
			.WithWebHostBuilder(builder => builder
				.ConfigureTestServices(services => services
				.AddElasticOpenTelemetry(options)
					.WithTracing(tpb => tpb.AddInMemoryExporter(exportedItems))));

		using var client = factory.CreateClient();

		using var response = await client.GetAsync("/");

		response.EnsureSuccessStatusCode();

		var snapshot = exportedItems.ToArray();

		if (snapshot.Length > 1)
		{
			_output.WriteLine($"{nameof(InstrumentationAssemblyScanning_AddsAspNetCoreInstrumentation)} > Exported items:");
			foreach (var item in snapshot)
			{
				_output.WriteLine($"- {item.DisplayName} : {item.OperationName} ; {item.Source.Name}");
			}
		}

		var activity = Assert.Single(snapshot, a => a.DisplayName.Equals("GET /", StringComparison.Ordinal));

		Assert.Equal("Microsoft.AspNetCore", activity.Source.Name);
	}

	[Fact]
	public async Task InstrumentationAssemblyScanning_AddsHttpInstrumentation()
	{
		// NOTE: When this runs on NET8, we expect the contrib library to be used.
		// On NET9+, the library dependency is not included with Elastic.OpenTelemetry,
		// so we expect the native instrumentation to be used.

		var exportedItems = new List<Activity>();
		var logger = new TestLogger(_output);
		var options = new ElasticOpenTelemetryOptions()
		{
			SkipOtlpExporter = true,
			AdditionalLogger = logger
		};

		// Explicitly not using a shared fixture for this test to avoid potential cross-test pollution
		// of the exported items collection, which would make assertions more difficult to write and maintain.
#if NET8_0
		await using var factory = new WebApplicationFactory<WebApiDotNet8.ProgramV8>()
#elif NET9_0
		await using var factory = new WebApplicationFactory<WebApiDotNet9.ProgramV9>()
#else
		await using var factory = new WebApplicationFactory<Program>()
#endif
			.WithWebHostBuilder(builder => builder
				.ConfigureTestServices(services => services
				.AddElasticOpenTelemetry(options)
					.WithTracing(tpb => tpb.AddInMemoryExporter(exportedItems))));

		using var client = factory.CreateClient();

		using var response = await client.GetAsync("/http");

		response.EnsureSuccessStatusCode();

		var snapshot = exportedItems.ToArray();

		if (snapshot.Length > 2)
		{
			_output.WriteLine($"{nameof(InstrumentationAssemblyScanning_AddsHttpInstrumentation)} > Exported items:");
			foreach (var item in snapshot)
			{
				_output.WriteLine($"- {item.DisplayName} : {item.OperationName} ; {item.Source.Name}");
			}
		}

		Assert.Equal(2, snapshot.Length); // One for ASP.NET Core and one for HTTP

		var activity = Assert.Single(snapshot, a => a.DisplayName.Equals("GET", StringComparison.Ordinal));

		Assert.Equal("System.Net.Http", activity.Source.Name);

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
