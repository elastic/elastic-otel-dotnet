// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Extensions;
using OpenTelemetry;
using Xunit.Abstractions;

namespace Elastic.OpenTelemetry.Tests;

public class LoggingTests(ITestOutputHelper output)
{
	[Fact]
	public async Task ObserveLogging()
	{
		var logger = new TestLogger(output);
		var options = new ElasticOpenTelemetryBuilderOptions { Logger = logger, DistroOptions = new() { SkipOtlpExporter = true } };
		const string activitySourceName = nameof(ObserveLogging);

		var activitySource = new ActivitySource(activitySourceName, "1.0.0");

		await using (new ElasticOpenTelemetryBuilder(options)
						 .WithTracing(tpb => tpb
							 .ConfigureResource(rb => rb.AddService("Test", "1.0.0"))
							 .AddSource(activitySourceName)
							 .AddInMemoryExporter([])
						 )
						 .Build())
		{
			using var activity = activitySource.StartActivity(ActivityKind.Internal);
			activity?.SetStatus(ActivityStatusCode.Ok);
		}

		//assert preamble information gets logged
		logger.Messages.Should().ContainMatch("*Elastic Distribution of OpenTelemetry .NET:*");

		var preambles = logger.Messages.Where(l => l.Contains("[Information]  Elastic Distribution of OpenTelemetry .NET:")).ToList();
		preambles.Should().NotBeNull().And.HaveCount(1);

		// assert instrumentation session logs initialized and stack trace gets dumped.
		logger.Messages.Should().ContainMatch("*ElasticOpenTelemetryBuilder initialized*");

		// very lenient format check
		logger.Messages.Should().ContainMatch("[*][*][*][Information]*");
	}
}
