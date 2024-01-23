// Licensed to Elasticsearch B.V under
// one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;

namespace Elastic.OpenTelemetry.IntegrationTests.DistributedFixture;

public class DistributedApplicationFixture : IDisposable, IAsyncLifetime
{
	private readonly AspNetCoreExampleApplication _aspNetApplication;
	private readonly ITrafficSimulator[] _trafficSimulators;

	public DistributedApplicationFixture()
	{
		ServiceName = $"dotnet-e2e-{ShaForCurrentTicks()}";
		HttpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5247") };

		var configuration = new ConfigurationBuilder()
			.AddEnvironmentVariables()
			.AddUserSecrets<DotNetRunApplication>()
			.Build();

		_aspNetApplication = new AspNetCoreExampleApplication(ServiceName, configuration);
		_trafficSimulators =
		[
			new DefaultTrafficSimulator()
		];
		ApmUIContext = new ApmUIBrowserContext(configuration, ApmKibanaUrl);
	}

	public ApmUIBrowserContext ApmUIContext { get; }

	public Uri ApmKibanaUrl => _aspNetApplication.ApmKibanaUrl;

	public HttpClient HttpClient { get; }

	public string ServiceName { get; }

	public bool Started => _aspNetApplication.ProcessId.HasValue;

	private static string ShaForCurrentTicks()
	{
		var buffer = Encoding.UTF8.GetBytes(DateTime.UtcNow.Ticks.ToString(DateTimeFormatInfo.InvariantInfo));

		return BitConverter.ToString(SHA1.Create().ComputeHash(buffer))
			.Replace("-", "")
			.ToLowerInvariant()
			.Substring(0, 12);
	}

	public void Dispose()
	{
		_aspNetApplication.Dispose();
		HttpClient.Dispose();
	}

	public async Task InitializeAsync()
	{
		foreach (var trafficSimulator in _trafficSimulators)
			await trafficSimulator.Start(ServiceName, HttpClient);

		// TODO query OTEL_BSP_SCHEDULE_DELAY?
		await Task.Delay(5000);

		// Stateless refresh
		//https://github.com/elastic/elasticsearch/blob/main/server/src/main/java/org/elasticsearch/index/IndexSettings.java#L286
		await Task.Delay(TimeSpan.FromSeconds(15));
		await ApmUIContext.InitializeAsync();
	}

	public async Task DisposeAsync()
	{
		Dispose();
		await ApmUIContext.DisposeAsync();
	}
}

public class AspNetCoreExampleApplication(string serviceName, IConfiguration configuration)
	: DotNetRunApplication(serviceName, configuration, "Example.Elastic.OpenTelemetry.AspNetCore");


public class ApmUIBrowserContext(IConfigurationRoot configuration, Uri kibanaUrl) : IAsyncLifetime
{

	public IPage Page { get; private set; } = null!;
	public IBrowser Browser { get; private set; } = null!;
	public IPlaywright HeadlessTester { get; private set; } = null!;

	public async Task InitializeAsync()
	{
		var username = configuration["E2E:BrowserEmail"]?.Trim() ?? string.Empty;
		var password = configuration["E2E:BrowserPassword"]?.Trim() ?? string.Empty;
		Program.Main(["install", "chromium"]);
		HeadlessTester = await Playwright.CreateAsync();
		Browser = await HeadlessTester.Chromium.LaunchAsync();
		Page = await Browser.NewPageAsync();
		await Page.GotoAsync(kibanaUrl.ToString());

		await Page.GetByRole(AriaRole.Textbox, new () { Name = "email" }).FillAsync(username);
		await Page.GetByRole(AriaRole.Textbox, new () { Name = "password" }).FillAsync(password);
		await Page.GetByRole(AriaRole.Button, new() { Name = "Log in" }).ClickAsync();
	}

	public async Task DisposeAsync()
	{
		await Browser.DisposeAsync();
		HeadlessTester.Dispose();
	}
}
