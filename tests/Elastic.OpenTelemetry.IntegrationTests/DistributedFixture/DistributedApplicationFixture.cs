// Licensed to Elasticsearch B.V under
// one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Elastic.OpenTelemetry.IntegrationTests.DistributedFixture;

public class DistributedApplicationFixture : IDisposable, IAsyncLifetime
{
	private readonly AspNetCoreExampleApplication _aspNetApplication;
	private readonly ITrafficSimulator[] _trafficSimulators;

	public DistributedApplicationFixture()
	{
		ServiceName = $"dotnet-e2e-{ShaForCurrentTicks()}";
		HttpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5247") };
		_aspNetApplication = new AspNetCoreExampleApplication(ServiceName);
		_trafficSimulators =
		[
			new DefaultTrafficSimulator()
		];
	}

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
		await Task.Delay(TimeSpan.FromSeconds(10));
	}

	public Task DisposeAsync()
	{
		Dispose();
		return Task.CompletedTask;
	}
}

public class AspNetCoreExampleApplication(string serviceName)
	: DotNetRunApplication(serviceName, "Example.Elastic.OpenTelemetry.AspNetCore");

public interface ITrafficSimulator
{
	Task Start(string serviceName, HttpClient client);
}

public class DefaultTrafficSimulator : ITrafficSimulator
{
	public async Task Start(string serviceName, HttpClient client)
	{
		for (var i = 0; i < 10; i++)
		{
			var get = await client.GetAsync("e2e");
			get.StatusCode.Should().Be(HttpStatusCode.OK);
			var response = await get.Content.ReadAsStringAsync();
		}
	}
}
