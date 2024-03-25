// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Extensions;
using OpenTelemetry;
using Xunit.Abstractions;

namespace Elastic.OpenTelemetry.Tests;

public class TransactionIdProcessorTests(ITestOutputHelper output)
{
	[Fact]
	public void TransactionId_IsAddedToTags()
	{
		var options = new ElasticOpenTelemetryOptions { Logger = new TestLogger(output), SkipOtlpExporter = true };
		const string activitySourceName = nameof(TransactionId_IsAddedToTags);

		var activitySource = new ActivitySource(activitySourceName, "1.0.0");

		var exportedItems = new List<Activity>();

		using var agent = new ElasticOpenTelemetryBuilder(options)
			.WithTracing(tpb =>
			{
				tpb
					.ConfigureResource(rb => rb.AddService("Test", "1.0.0"))
					.AddSource(activitySourceName)
					.AddInMemoryExporter(exportedItems);
			})
			.Build();

		using (var activity = activitySource.StartActivity(ActivityKind.Internal))
			activity?.SetStatus(ActivityStatusCode.Ok);

		exportedItems.Should().HaveCount(1);

		var exportedActivity = exportedItems[0];

		var transactionId = exportedActivity.GetTagItem(TransactionIdProcessor.TransactionIdTagName);

		transactionId.Should().NotBeNull().And.BeAssignableTo<string>().Which.Should().NotBeEmpty();
	}
}
