// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;
using Xunit;

namespace Elastic.OpenTelemetry.EndToEndTests.DistributedFixture;

public class ApmUIBrowserContext : IAsyncLifetime
{
	private readonly IConfigurationRoot _configuration;
	private readonly string _serviceName;

	public ApmUIBrowserContext(IConfigurationRoot configuration, string serviceName)
	{
		_configuration = configuration;
		_serviceName = serviceName;
		//"https://{instance}.apm.us-east-1.aws.elastic.cloud:443"
		// https://{instance}.kb.us-east-1.aws.elastic.cloud/app/apm/services?comparisonEnabled=true&environment=ENVIRONMENT_ALL&rangeFrom=now-15m&rangeTo=now&offset=1d
		var endpoint = configuration["E2E:Endpoint"]?.Trim() ?? string.Empty;
		var newBase = endpoint.Replace(".apm.", ".kb.");
		KibanaAppUri = new Uri(new Uri(newBase), "app/apm");
	}

	public Uri KibanaAppUri { get; }

	public IBrowser Browser { get; private set; } = null!;
	public IPlaywright HeadlessTester { get; private set; } = null!;

	public async Task InitializeAsync()
	{
		var username = _configuration["E2E:BrowserEmail"]?.Trim() ?? string.Empty;
		var password = _configuration["E2E:BrowserPassword"]?.Trim() ?? string.Empty;
		Program.Main(["install", "chromium"]);
		HeadlessTester = await Playwright.CreateAsync();
		Browser = await HeadlessTester.Chromium.LaunchAsync();
		var page = await OpenApmLandingPage("test_bootstrap");
		try
		{
			await page.GetByRole(AriaRole.Textbox, new () { Name = "email" }).FillAsync(username);
			await page.GetByRole(AriaRole.Textbox, new () { Name = "password" }).FillAsync(password);
			await page.GetByRole(AriaRole.Button, new() { Name = "Log in" }).ClickAsync();

			await WaitForServiceOnOverview(page);

			StorageState = await page.Context.StorageStateAsync();

			await StopTrace(page);
		}
		catch (Exception e)
		{
			await StopTrace(page, "test_bootstrap");
			Console.WriteLine(e);
			throw;
		}
	}

	public string? StorageState { get; set; }


	public async Task<IPage> NewProfiledPage(string testName)
	{
		var page = await Browser.NewPageAsync(new () { StorageState = StorageState });
		await page.Context.Tracing.StartAsync(new()
		{
			Title = testName,
			Screenshots = true,
			Snapshots = true,
			Sources = true
		});

		return page;
	}


	public async Task<IPage> OpenApmLandingPage(string testName)
	{
		var page = await NewProfiledPage(testName);
		await page.GotoAsync(KibanaAppUri.ToString());
		return page;
	}

	public async Task WaitForServiceOnOverview(IPage page)
	{
		page.SetDefaultTimeout((float)TimeSpan.FromSeconds(30).TotalMilliseconds);

		var servicesHeader = page.GetByRole(AriaRole.Heading, new() { Name = "Services" });
		await servicesHeader.WaitForAsync(new () { State = WaitForSelectorState.Visible });

		page.SetDefaultTimeout((float)TimeSpan.FromSeconds(10).TotalMilliseconds);

		Exception? observed = null;
		for (var i = 0; i < 10;i++)
		{
			try
			{
				var serviceLink = page.GetByRole(AriaRole.Link, new() { Name = _serviceName });
				await serviceLink.WaitForAsync(new() { State = WaitForSelectorState.Visible });
				observed = null;
				break;
			}
			catch (Exception e)
			{
				observed ??= e;
				await page.ReloadAsync();
			}
			finally
			{
				page.SetDefaultTimeout((float)TimeSpan.FromSeconds(5).TotalMilliseconds);
			}
		}
		if (observed != null)
			throw observed; //TODO proper rethrow with stack

	}

	public async Task StopTrace(IPage page, string? testName = null)
	{

		if (string.IsNullOrWhiteSpace(testName))
			await page.Context.Tracing.StopAsync(new ());
		else
		{
			var root = DotNetRunApplication.GetSolutionRoot();
			await page.Context.Tracing.StopAsync(new()
			{
				Path = Path.Combine(
					Path.Combine(root.FullName, ".artifacts"),
					"playwright-traces",
					$"{testName}.zip"
				)
			});
		}
		await page.CloseAsync();
	}


	public async Task DisposeAsync()
	{
		await Browser.DisposeAsync();
		HeadlessTester.Dispose();
	}
}
