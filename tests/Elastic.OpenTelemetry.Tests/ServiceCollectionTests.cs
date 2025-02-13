// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Core;
using Elastic.OpenTelemetry.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using Xunit.Abstractions;

namespace Elastic.OpenTelemetry.Tests;

public class ServiceCollectionTests(ITestOutputHelper output)
{
	[Fact]
	public async Task ServiceCollection_AddOpenTelemetry_IsSafeToCallMultipleTimes()
	{
		const string activitySourceName = nameof(ServiceCollection_AddOpenTelemetry_IsSafeToCallMultipleTimes);
		var activitySource = new ActivitySource(activitySourceName, "1.0.0");

		var exportedItems = new List<Activity>();

		var host = Host.CreateDefaultBuilder();
		host.ConfigureServices(s =>
		{
			var options = new ElasticOpenTelemetryOptions()
			{
				SkipOtlpExporter = true,
				AdditionalLogger = new TestLogger(output)
			};

			s.AddElasticOpenTelemetry(options)
				.WithTracing(tpb => tpb
					.ConfigureResource(rb => rb.AddService("Test", "1.0.0"))
					.AddSource(activitySourceName)
					.AddInMemoryExporter(exportedItems)
				);

			s.AddOpenTelemetry();
			s.AddOpenTelemetry();
			s.AddOpenTelemetry();
		});

		var ctx = new CancellationTokenRegistration();
		using (var app = host.Build())
		{
			_ = app.RunAsync(ctx.Token);
			using (var activity = activitySource.StartActivity(ActivityKind.Internal))
				activity?.SetStatus(ActivityStatusCode.Ok);
			await ctx.DisposeAsync();
		}

		Assert.Single(exportedItems);
	}

	[Fact]
	public async Task ServiceCollection_AddElasticOpenTelemetry_IsSafeToCallMultipleTimes()
	{
		const string activitySourceName = nameof(ServiceCollection_AddElasticOpenTelemetry_IsSafeToCallMultipleTimes);
		var activitySource = new ActivitySource(activitySourceName, "1.0.0");

		var exportedItems = new List<Activity>();

		var host = Host.CreateDefaultBuilder();
		host.ConfigureServices(s =>
		{
			var options = new ElasticOpenTelemetryOptions()
			{
				SkipOtlpExporter = true,
				AdditionalLogger = new TestLogger(output)
			};

			s.AddElasticOpenTelemetry(options)
				.WithTracing(tpb => tpb
					.ConfigureResource(rb => rb.AddService("Test", "1.0.0"))
					.AddSource(activitySourceName)
					.AddInMemoryExporter(exportedItems)
				);

			s.AddElasticOpenTelemetry();
			s.AddElasticOpenTelemetry();
			s.AddElasticOpenTelemetry();
		});

		var ctx = new CancellationTokenRegistration();
		using (var app = host.Build())
		{
			_ = app.RunAsync(ctx.Token);
			using (var activity = activitySource.StartActivity(ActivityKind.Internal))
				activity?.SetStatus(ActivityStatusCode.Ok);
			await ctx.DisposeAsync();
		}

		Assert.Single(exportedItems);
	}

	[Fact]
	public void ServiceCollectionAddElasticOpenTelemetry_ReturnsSameComponents_WhenCalledMultipleTimes()
	{
		// Ensure that when AddElasticOpenTelemetry is called multiple times on the same IServiceCollection,
		// a single instance of the components is registered, as we expect those to be cached per IServiceCollection.
		// Even though each call operates on a new `OpenTelemetryBuilder`, our code is designed to reduce accidental,
		// duplication of bootstrapping in such scenarios.

		var serviceCollection = new ServiceCollection();

		serviceCollection.AddElasticOpenTelemetry();

		var initialComponents = serviceCollection.Single(d => d.ServiceType == typeof(ElasticOpenTelemetryComponents)).ImplementationInstance;

		serviceCollection.AddElasticOpenTelemetry();

		using var serviceProvider = serviceCollection.BuildServiceProvider();

		var components = serviceProvider.GetServices<ElasticOpenTelemetryComponents>();

		Assert.Single(components);
		Assert.Same(initialComponents, components.Single());

		var hostedService = serviceProvider.GetServices<IHostedService>()
			.Where(t => t is ElasticOpenTelemetryService)
			.Cast<ElasticOpenTelemetryService>();

		Assert.Single(hostedService);
	}
}
