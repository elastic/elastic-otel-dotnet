// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Elastic.OpenTelemetry.IntegrationTests.DistributedFixture;

public class DistributedApplicationFixture : IDisposable, IAsyncLifetime
{
	public AspNetCoreExampleApplication AspNetApplication { get; }

	private readonly ITrafficSimulator[] _trafficSimulators;

	public DistributedApplicationFixture()
	{
		ServiceName = $"dotnet-e2e-{ShaForCurrentTicks()}";

		var configuration = new ConfigurationBuilder()
			.AddEnvironmentVariables()
			.AddUserSecrets<DotNetRunApplication>()
			.Build();
		_trafficSimulators = [ new DefaultTrafficSimulator() ];

		AspNetApplication = new AspNetCoreExampleApplication(ServiceName, configuration);
		ApmUI = new ApmUIBrowserContext(configuration, ServiceName);
	}

	public ApmUIBrowserContext ApmUI { get; }

	public string ServiceName { get; }

	public bool Started => AspNetApplication.ProcessId.HasValue;

	private static string ShaForCurrentTicks()
	{
		var buffer = Encoding.UTF8.GetBytes(DateTime.UtcNow.Ticks.ToString(DateTimeFormatInfo.InvariantInfo));

		return BitConverter.ToString(SHA1.Create().ComputeHash(buffer))
			.Replace("-", "")
			.ToLowerInvariant()
			.Substring(0, 12);
	}

	public void Dispose() => AspNetApplication.Dispose();

	public async Task InitializeAsync()
	{
		foreach (var trafficSimulator in _trafficSimulators)
			await trafficSimulator.Start(this);

		// TODO query OTEL_BSP_SCHEDULE_DELAY?
		await Task.Delay(5000);

		// Stateless refresh
		//https://github.com/elastic/elasticsearch/blob/main/server/src/main/java/org/elasticsearch/index/IndexSettings.java#L286
		await Task.Delay(TimeSpan.FromSeconds(15));
		await ApmUI.InitializeAsync();
	}

	public async Task DisposeAsync()
	{
		Dispose();
		await ApmUI.DisposeAsync();
	}
}

public class AspNetCoreExampleApplication : DotNetRunApplication
{
	public AspNetCoreExampleApplication(string serviceName, IConfiguration configuration)
		: base(serviceName, configuration, "Example.Elastic.OpenTelemetry.AspNetCore") =>
		HttpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5247") };

	public HttpClient HttpClient { get; }

	public override void Dispose()
	{
		base.Dispose();
		HttpClient.Dispose();
	}
};
