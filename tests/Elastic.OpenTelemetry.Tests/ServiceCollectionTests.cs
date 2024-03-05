// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using Xunit.Abstractions;

namespace Elastic.OpenTelemetry.Tests;

public class ServiceCollectionTests(ITestOutputHelper output)
{
    [Fact]
    public async Task ServiceCollectionAddIsSafeToCallMultipleTimes()
	{
		var options = new AgentBuilderOptions { Logger = new TestLogger(output), SkipOtlpExporter = true };

        const string activitySourceName = nameof(ServiceCollectionAddIsSafeToCallMultipleTimes);
        var activitySource = new ActivitySource(activitySourceName, "1.0.0");

        var exportedItems = new List<Activity>();

		var host = Host.CreateDefaultBuilder();
		host.ConfigureServices(s =>
		{
			s.AddOpenTelemetry(options)
				.SkipOtlpExporter()
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


        exportedItems.Should().HaveCount(1);
    }
}
