// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.EndToEndTests.DistributedFixture;
using Microsoft.Playwright;
using Nullean.Xunit.Partitions.Sdk;
using Xunit;
using Xunit.Abstractions;
using static Microsoft.Playwright.Assertions;

namespace Elastic.OpenTelemetry.EndToEndTests;

public class EndToEndTests(ITestOutputHelper output, DistributedApplicationFixture fixture)
	: IPartitionFixture<DistributedApplicationFixture>, IAsyncLifetime
{
	public ITestOutputHelper Output { get; } = output;
	private readonly string _testName = string.Empty;
	private IPage _page = null!;

	[Fact]
	public void EnsureApplicationWasStarted() => Assert.True(fixture.Started);

	[Fact]
	public async Task LatencyShowsAGraph()
	{
		var timeout = (float)TimeSpan.FromSeconds(30).TotalMilliseconds;

		// click on service in service overview page.
		var uri = new Uri(fixture.ApmUI.KibanaAppUri, $"/app/apm/services/{fixture.ServiceName}/overview").ToString();
		await _page.GotoAsync(uri, new() { Timeout = timeout });
		await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Latency", Exact = true }))
			.ToBeVisibleAsync(new() { Timeout = timeout });
	}

	public async Task InitializeAsync() => _page = await fixture.ApmUI.NewProfiledPage(_testName);

	public async Task DisposeAsync()
	{
		var success = PartitionContext.TestException == null;
		await fixture.ApmUI.StopTrace(_page, success, _testName);

		if (success)
			return;

		fixture.WriteFailureTestOutput(Output);
	}
}
