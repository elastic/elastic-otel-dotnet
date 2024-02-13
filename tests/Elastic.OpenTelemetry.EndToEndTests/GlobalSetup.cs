// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.EndToEndTests;
using Elastic.OpenTelemetry.EndToEndTests.DistributedFixture;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Nullean.Xunit.Partitions;
using Xunit;

[assembly: TestFramework(Partition.TestFramework, Partition.Assembly)]
[assembly: PartitionOptions(typeof(EndToEndOptions))]

namespace Elastic.OpenTelemetry.EndToEndTests;

public class EndToEndOptions : PartitionOptions
{
	public EndToEndOptions() { }

	public override void OnBeforeTestsRun()
	{
		var configuration = new ConfigurationBuilder()
			.AddEnvironmentVariables()
			.AddUserSecrets<DotNetRunApplication>()
			.Build();

		var testSuite = Environment.GetEnvironmentVariable("TEST_SUITE");

		//only validate credentials if we are actually running the e2e suite
		if (testSuite == null || (
				!testSuite.Equals("e2e", StringComparison.InvariantCultureIgnoreCase)
				&& !testSuite.Equals("all", StringComparison.InvariantCultureIgnoreCase))
			)
			return;

		try
		{
			configuration["E2E:Endpoint"].Should()
				.NotBeNullOrWhiteSpace("Missing E2E:Endpoint configuration");
			configuration["E2E:Authorization"].Should()
				.NotBeNullOrWhiteSpace("Missing E2E:Authorization configuration");
			configuration["E2E:BrowserEmail"].Should()
				.NotBeNullOrWhiteSpace("Missing E2E:BrowserEmail configuration");
			configuration["E2E:BrowserPassword"].Should()
				.NotBeNullOrWhiteSpace("Missing E2E:BrowserPassword configuration");
		}
		catch (Exception e)
		{
			Console.WriteLine();
			Console.WriteLine(e.Message);
			Console.WriteLine();
			throw;
		}
	}
}
