// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Xunit;
using FluentAssertions;
using Nullean.Xunit.Partitions.Sdk;

namespace Elastic.OpenTelemetry.AutoInstrumentationTests;

public class PluginLoaderTests(ExampleApplicationContainer exampleApplicationContainer) : IPartitionFixture<ExampleApplicationContainer>
{

	[Fact]
	public async Task ObserveDistributionPluginLoad()
	{
		await Task.Delay(TimeSpan.FromSeconds(3));
		var output = exampleApplicationContainer.FailureTestOutput();
		output.Should()
			.NotBeNullOrWhiteSpace()
			.And.Contain("Elastic OpenTelemetry Distribution:")
			.And.Contain("ElasticOpenTelemetryBuilder initialized")
			.And.Contain("Added 'Elastic.OpenTelemetry.Processors.ElasticCompatibilityProcessor'");

	}

}
