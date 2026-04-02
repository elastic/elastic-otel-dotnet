// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using OpenTelemetry;
using Xunit.Abstractions;

namespace Elastic.OpenTelemetry.Tests;

[Collection("CompositeLoggerSingleton")]
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

		await Task.Delay(500); // Small delay to ensure the activity has been exported before we take the snapshot for assertions

		var snapshot = exportedItems.ToArray();

		if (snapshot.Length > 1)
		{
			_output.WriteLine($"{nameof(InstrumentationAssemblyScanning_AddsAspNetCoreInstrumentation)} > Exported items:");
			foreach (var item in snapshot)
			{
				_output.WriteLine($"- {item.DisplayName} : {item.OperationName} ; {item.Source.Name}");
			}
		}

		var activity = Assert.Single(snapshot, a =>
			a.Source.Name == "Microsoft.AspNetCore" &&
			a.DisplayName.Equals("GET /", StringComparison.Ordinal));
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

		await Task.Delay(500); // Small delay to ensure the activity has been exported before we take the snapshot for assertions

		var snapshot = exportedItems.ToArray();

		_output.WriteLine($"{nameof(InstrumentationAssemblyScanning_AddsHttpInstrumentation)} > Exported items ({snapshot.Length}):");
		foreach (var item in snapshot)
		{
			_output.WriteLine($"- {item.DisplayName} : {item.OperationName} ; {item.Source.Name}");
		}

		// We expect at least an ASP.NET Core activity and an HTTP activity.
		// The exact count may be higher when the OTel singleton captures activities
		// from other tests running in parallel, so avoid asserting on exact count.
		Assert.True(snapshot.Length >= 2, $"Expected at least 2 exported activities but found {snapshot.Length}.");

		// Filter by url.full tag to uniquely identify *this* test's HTTP activity,
		// avoiding collisions with activities from parallel tests sharing the OTel singleton.
		var activity = Assert.Single(snapshot, a =>
			a.Source.Name == "System.Net.Http" &&
			a.TagObjects.Any(t => t.Key == "url.full" && (string?)t.Value == "https://example.com/"));

		Assert.Equal("GET", activity.DisplayName);

		var foundExpectedHttpTracerInstrumentationMessage = false;
		var foundExpectedHttpMeterInstrumentationMessage = false;

		var messages = logger.Messages.ToArray();

		foreach (var message in messages)
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
