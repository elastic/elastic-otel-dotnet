// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Configuration;
using OpenTelemetry;
using Xunit.Abstractions;
using OpenTelemetryBuilderExtensions = Elastic.OpenTelemetry.Extensions.OpenTelemetryBuilderExtensions;

namespace Elastic.OpenTelemetry.Tests.Processors;

public class TransactionProcessorTests(ITestOutputHelper output)
{
	[Fact]
	public void TransactionId_IsNotAdded_WhenElasticDefaultsDoesNotIncludeTracing()
	{
		var options = new ElasticOpenTelemetryBuilderOptions
		{
			Logger = new TestLogger(output),
			DistroOptions = new ElasticOpenTelemetryOptions()
			{
				SkipOtlpExporter = true,
				EnabledDefaults = ElasticDefaults.None
			}
		};

		const string activitySourceName = nameof(TransactionId_IsNotAdded_WhenElasticDefaultsDoesNotIncludeTracing);

		var activitySource = new ActivitySource(activitySourceName, "1.0.0");

		var exportedItems = new List<Activity>();

		using var session = OpenTelemetryBuilderExtensions.Build(new ElasticOpenTelemetryBuilder(options)
				.WithTracing(tpb =>
				{
					tpb
						.ConfigureResource(rb => rb.AddService("Test", "1.0.0"))
						.AddSource(activitySourceName).AddInMemoryExporter(exportedItems);
				}));

		using (var activity = activitySource.StartActivity(ActivityKind.Internal))
			activity?.SetStatus(ActivityStatusCode.Ok);

		exportedItems.Should().ContainSingle();

		var exportedActivity = exportedItems[0];

		var transactionId = exportedActivity.GetTagItem(TransactionIdProcessor.TransactionIdTagName);

		transactionId.Should().BeNull();
	}
}
