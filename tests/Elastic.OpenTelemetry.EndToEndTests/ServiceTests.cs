// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.EndToEndTests.DistributedFixture;
using Microsoft.Playwright;
using Xunit.Abstractions;
using Xunit.Extensions.AssemblyFixture;
using static Microsoft.Playwright.Assertions;

namespace Elastic.OpenTelemetry.EndToEndTests;

public class EndToEndTests(ITestOutputHelper output, DistributedApplicationFixture fixture)
	: XunitContextBase(output), IAssemblyFixture<DistributedApplicationFixture>, IAsyncLifetime
{
	private string _testName = string.Empty;
	private IPage _page = null!;

	[Fact]
	public void EnsureApplicationWasStarted() => fixture.Started.Should().BeTrue();

	[Fact]
	public async Task LatencyShowsAGraph()
	{
		// click on service in service overview page.
		_page.SetDefaultTimeout((float)TimeSpan.FromSeconds(30).TotalMilliseconds);
		var uri = new Uri(fixture.ApmUI.KibanaAppUri, $"/app/apm/services/{fixture.ServiceName}/overview").ToString();
		await _page.GotoAsync(uri);
		await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Latency", Exact = true })).ToBeVisibleAsync();
	}


	public async Task InitializeAsync() => _page = await fixture.ApmUI.NewProfiledPage(_testName);

	public async Task DisposeAsync() => await fixture.ApmUI.StopTrace(_page, XunitContext.Context.TestException == null ? null : _testName);
}
