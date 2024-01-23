// Licensed to Elasticsearch B.V under
// one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.IntegrationTests.DistributedFixture;
using Microsoft.Playwright;
using Xunit.Extensions.AssemblyFixture;
using static Microsoft.Playwright.Assertions;

namespace Elastic.OpenTelemetry.IntegrationTests;

public class EndToEndTests(ITestOutputHelper output, DistributedApplicationFixture fixture)
	: XunitContextBase(output), IAssemblyFixture<DistributedApplicationFixture>, IAsyncLifetime
{
	public IPage Page => fixture.ApmUIContext.Page;
	private string _testName = string.Empty;

	[Fact]
	public async Task Test()
	{
		fixture.Started.Should().BeTrue();

		Page.SetDefaultTimeout((float)TimeSpan.FromSeconds(20).TotalMilliseconds);
		var servicesHeader = Page.GetByRole(AriaRole.Heading, new() { Name = "Services" });
		await servicesHeader.WaitForAsync(new () { State = WaitForSelectorState.Visible });

		var serviceLink = Page.GetByRole(AriaRole.Link, new() { Name = fixture.ServiceName });
		await serviceLink.WaitForAsync(new () { State = WaitForSelectorState.Visible });
		Page.SetDefaultTimeout((float)TimeSpan.FromSeconds(5).TotalMilliseconds);

		await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Get started" })).ToBeVisibleAsync();
	}

	public async Task InitializeAsync()
	{
		_testName = XunitContext.Context.ClassName + "." + XunitContext.Context.Test.DisplayName;
		await Page.Context.Tracing.StartAsync(new()
		{
			Title = _testName,
			Screenshots = true,
			Snapshots = true,
			Sources = true
		});
	}

	public async Task DisposeAsync() =>
		await Page.Context.Tracing.StopAsync(new()
		{
			Path = Path.Combine(
				Path.Combine(XunitContext.Context.SolutionDirectory, ".artifacts"),
				"playwright-traces",
				$"{_testName}.zip"
			)
		});
}
