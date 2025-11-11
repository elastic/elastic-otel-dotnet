// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;
using Xunit;

namespace Elastic.OpenTelemetry.EndToEndTests.DistributedFixture;

public class ApmUIBrowserContext : IAsyncLifetime
{
	private readonly IConfigurationRoot _configuration;
	private readonly string _serviceName;
	private readonly string _playwrightScreenshotsDir;
	private readonly List<string> _output;

	public ApmUIBrowserContext(IConfigurationRoot configuration, string serviceName, string playwrightScreenshotsDir, List<string> output)
	{
		_configuration = configuration;
		_serviceName = serviceName;
		_playwrightScreenshotsDir = playwrightScreenshotsDir;
		_output = output;

		//"https://{instance}.apm.us-east-1.aws.elastic.cloud:443"
		// https://{instance}.kb.us-east-1.aws.elastic.cloud/app/apm/services?comparisonEnabled=true&environment=ENVIRONMENT_ALL&rangeFrom=now-15m&rangeTo=now&offset=1d
		var endpoint = configuration["E2E:Endpoint"]?.Trim() ?? string.Empty;
		var newBase = endpoint.Replace(".apm.", ".kb.");
		KibanaAppUri = new Uri($"{newBase}/app/apm");
	}

	public Uri KibanaAppUri { get; }

	public IBrowser Browser { get; private set; } = null!;
	public IPlaywright HeadlessTester { get; private set; } = null!;

	private const string BootstrapTraceName = "test_bootstrap";

	public async Task InitializeAsync()
	{
		var username = _configuration["E2E:BrowserEmail"]?.Trim() ?? string.Empty;
		var password = _configuration["E2E:BrowserPassword"]?.Trim() ?? string.Empty;
		Program.Main(["install", "chromium"]);
		HeadlessTester = await Playwright.CreateAsync();
		Browser = await HeadlessTester.Chromium.LaunchAsync();
		var page = await OpenApmLandingPage(BootstrapTraceName);
		try
		{
			await page.GetByRole(AriaRole.Textbox, new() { Name = "email" }).FillAsync(username);
			await page.GetByRole(AriaRole.Textbox, new() { Name = "password" }).FillAsync(password);
			await page.GetByRole(AriaRole.Button, new() { Name = "Log in" }).ClickAsync();

			await WaitForServiceOnOverview(page);

			StorageState = await page.Context.StorageStateAsync();

			await StopTrace(page, success: true);
		}
		catch (Exception e)
		{
			await StopTrace(page, success: false);
			Console.WriteLine(e);
			throw;
		}
	}

	public string? StorageState { get; set; }

	public async Task<IPage> NewProfiledPage(string testName)
	{
		var page = await Browser.NewPageAsync(new() { StorageState = StorageState });
		await page.Context.Tracing.StartAsync(new()
		{
			Title = testName,
			Screenshots = true,
			Snapshots = true,
			Sources = false
		});

		return page;
	}

	public async Task<IPage> OpenApmLandingPage(string testName)
	{
		var page = await NewProfiledPage(testName);
		await page.GotoAsync(KibanaAppUri.ToString());
		return page;
	}

	private void Log(string message)
	{
		Console.WriteLine(message);
		_output.Add(message);
	}

	public async Task WaitForServiceOnOverview(IPage page)
	{
		var timeout = (float)TimeSpan.FromSeconds(30).TotalMilliseconds;

		var servicesHeader = page.GetByRole(AriaRole.Heading, new() { Name = "Services" });
		await servicesHeader.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeout });

		await page.ScreenshotAsync(new() { Path = Path.Join(_playwrightScreenshotsDir, "services-loaded.jpeg"), FullPage = true });

		Log($"Search for service name: {_serviceName}");

		//service.name : dotnet-e2e-*
		var queryBar = page.GetByRole(AriaRole.Searchbox, new() { Name = "Search services by name" });
		await queryBar.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeout });
		await queryBar.FillAsync(_serviceName);
		await queryBar.PressAsync("Enter");

		await page.ScreenshotAsync(new() { Path = Path.Join(_playwrightScreenshotsDir, "filter-services.jpeg"), FullPage = true });

		Exception? observed = null;

		var refreshTimeout = (float)TimeSpan.FromSeconds(5).TotalMilliseconds;
		for (var i = 0; i < 20; i++)
		{
			try
			{
				var serviceLink = page.GetByRole(AriaRole.Link, new() { Name = _serviceName });
				await serviceLink.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = refreshTimeout });
				observed = null;
				break;
			}
			catch (Exception e)
			{
				observed ??= e;
			}
			finally
			{
				page.SetDefaultTimeout(refreshTimeout);
			}
		}
		if (observed != null)
			throw observed; //TODO proper rethrow with stack
	}

	private int _unnamedTests;
	public async Task StopTrace(IPage page, bool success, [CallerMemberName] string? testName = null)
	{
		testName ??= $"unknown_test_{_unnamedTests++}";

		//only dump trace zip if tests failed
		if (success)
		{
			await page.Context.Tracing.StopAsync(new());
		}
		else
		{
			var root = DotNetRunApplication.GetSolutionRoot();
			var zip = Path.Combine(root.FullName, ".artifacts", "playwright-traces", $"{testName}.zip");
			await page.Context.Tracing.StopAsync(new() { Path = zip });

			//using var archive = ZipFile.OpenRead(zip);
			//var entries = archive.Entries.Where(e => e.FullName.StartsWith("resources") && e.FullName.EndsWith(".jpeg")).ToList();
			//var lastScreenshot = entries.MaxBy(e => e.LastWriteTime);
			//lastScreenshot?.ExtractToFile(Path.Combine(root.FullName, ".artifacts", "playwright-traces", $"{testName}-screenshot.jpeg"));
		}
		await page.CloseAsync();
	}

	public async Task DisposeAsync()
	{
		await Browser.DisposeAsync();
		HeadlessTester.Dispose();
	}
}
