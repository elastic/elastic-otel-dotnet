// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Nullean.Xunit.Partitions.Sdk;
using Xunit.Abstractions;

namespace Elastic.OpenTelemetry.EndToEndTests.DistributedFixture;

public class DistributedApplicationFixture : IPartitionLifetime
{
	private readonly ITrafficSimulator[] _trafficSimulators = [new DefaultTrafficSimulator()];

	public string ServiceName { get; } = $"dotnet-e2e-{ShaForCurrentTicks()}";

	public bool Started => AspNetApplication.ProcessId.HasValue;

	public string PlaywrightScreenshotsDirectory { get; } = Path.Combine(DotNetRunApplication.GetSolutionRoot().FullName, ".artifacts", "playwright-traces", "screenshots");

	private readonly List<string> _output = [];

	public int? MaxConcurrency => null;

	private ApmUIBrowserContext? _apmUI;

	public ApmUIBrowserContext ApmUI
	{
		get => _apmUI ??
			throw new NullReferenceException($"{nameof(DistributedApplicationFixture)} no yet initialized");
		private set => _apmUI = value;
	}

	private AspNetCoreExampleApplication? _aspNetApplication;

	public AspNetCoreExampleApplication AspNetApplication
	{
		get => _aspNetApplication
			?? throw new NullReferenceException($"{nameof(DistributedApplicationFixture)} no yet initialized");
		private set => _aspNetApplication = value;
	}

	private static string ShaForCurrentTicks()
	{
		var buffer = Encoding.UTF8.GetBytes(DateTime.UtcNow.Ticks.ToString(DateTimeFormatInfo.InvariantInfo));
		return Convert.ToHexStringLower(SHA1.HashData(buffer)).Substring(0, 12);
	}

	public string FailureTestOutput()
	{
		var logLines = new List<string>();
		if (_aspNetApplication?.ProcessId.HasValue ?? false)
		{
			DotNetRunApplication.IterateOverLog(s =>
			{
				Console.WriteLine(s);
				logLines.Add(s);
			});
		}

		var messages = string.Join(Environment.NewLine, _output.Concat(logLines));
		return messages;

	}

	public void WriteFailureTestOutput(ITestOutputHelper testOutputHelper)
	{
		foreach (var line in _output)
			testOutputHelper.WriteLine(line);

		DotNetRunApplication.IterateOverLog(s =>
		{
			Console.WriteLine(s);
			testOutputHelper.WriteLine(s);
		});
	}

	public async Task DisposeAsync()
	{
		_aspNetApplication?.Dispose();
		await (_apmUI?.DisposeAsync() ?? Task.CompletedTask);
	}

	private void Log(string message)
	{
		Console.WriteLine(message);
		_output.Add(message);
	}

	public async Task InitializeAsync()
	{
		var configuration = new ConfigurationBuilder()
			.AddEnvironmentVariables()
			.AddUserSecrets<DotNetRunApplication>()
			.Build();

		Log("Created configuration");

		AspNetApplication = new AspNetCoreExampleApplication(ServiceName, configuration);

		Log("Started ASP.NET application");

		ApmUI = new ApmUIBrowserContext(configuration, ServiceName, PlaywrightScreenshotsDirectory, _output);

		Log("Started UI Browser context");

		foreach (var trafficSimulator in _trafficSimulators)
			await trafficSimulator.Start(this);

		Log("Simulated traffic");

		// TODO query OTEL_BSP_SCHEDULE_DELAY?
		await Task.Delay(5000);

		Log("Waited for OTEL_BSP_SCHEDULE_DELAY");

		// Stateless refresh
		//https://github.com/elastic/elasticsearch/blob/main/server/src/main/java/org/elasticsearch/index/IndexSettings.java#L286
		await Task.Delay(TimeSpan.FromSeconds(15));

		Log("Waited for Stateless refresh");

		await ApmUI.InitializeAsync();
	}
}

public class AspNetCoreExampleApplication : DotNetRunApplication
{
	public AspNetCoreExampleApplication(string serviceName, IConfiguration configuration)
		: base(serviceName, configuration, "Example.AspNetCore.Mvc") =>
		HttpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5247") };

	public HttpClient HttpClient { get; }

	public override void Dispose()
	{
		base.Dispose();
		HttpClient.Dispose();
	}
};
