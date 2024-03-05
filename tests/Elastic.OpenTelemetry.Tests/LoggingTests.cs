// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections.ObjectModel;
using OpenTelemetry;
using Xunit.Abstractions;

namespace Elastic.OpenTelemetry.Tests;

public class LoggingTests(ITestOutputHelper output)
{
	[Fact]
	public async Task ObserveLogging()
	{
		var logger = new TestLogger(output);
		const string activitySourceName = nameof(ObserveLogging);

		var activitySource = new ActivitySource(activitySourceName, "1.0.0");

		await using (new AgentBuilder(logger)
						 .SkipOtlpExporter()
						 .WithTracing(tpb => tpb
							 .ConfigureResource(rb => rb.AddService("Test", "1.0.0"))
							 .AddSource(activitySourceName)
							 .AddInMemoryExporter(new List<Activity>())
						 )
						 .Build())
		{
			using (var activity = activitySource.StartActivity("DoingStuff", ActivityKind.Internal))
			{
				activity?.SetStatus(ActivityStatusCode.Ok);
			}
		}

		//assert preamble information gets logged
		logger.Messages.Should().ContainMatch("*Elastic OpenTelemetry Distribution:*");

		var preambles = logger.Messages.Where(l => l.Contains("[Info]      Elastic OpenTelemetry Distribution:")).ToList();
		preambles.Should().HaveCount(1);

		// assert agent initialized confirmation and stack trace gets dumped.
		logger.Messages.Should().ContainMatch("*AgentBuilder initialized*");

		// very lenient format check
		logger.Messages.Should().ContainMatch("[*][*][*][Info]*");
	}
}
