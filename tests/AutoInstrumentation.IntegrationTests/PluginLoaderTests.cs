// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Runtime.InteropServices;
using Nullean.Xunit.Partitions.Sdk;
using Xunit;

namespace Elastic.OpenTelemetry.AutoInstrumentation.IntegrationTests;

public class PluginLoaderTests(ExampleApplicationContainer exampleApplicationContainer) : IPartitionFixture<ExampleApplicationContainer>
{
	[NotWindowsCiFact]
	public async Task ObserveDistributionPluginLoad()
	{
		await Task.Delay(TimeSpan.FromSeconds(3));

		var output = exampleApplicationContainer.FailureTestOutput();

		Assert.False(string.IsNullOrWhiteSpace(output));
		Assert.Contains("Elastic Distribution of OpenTelemetry (EDOT) .NET:", output);
		Assert.Contains("Elastic OpenTelemetry components created.", output);
	}
}

public class NotWindowsCiFact : FactAttribute
{
	public NotWindowsCiFact()
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")))
			Skip = "We can not run this test in a virtualized windows environment";
	}
}
