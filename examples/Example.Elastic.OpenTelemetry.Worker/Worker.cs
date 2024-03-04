// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Example.Elastic.OpenTelemetry.Worker;

public class Worker(ILogger<Worker> logger) : BackgroundService
{
	private readonly ILogger<Worker> _logger = logger;

	private static readonly HttpClient HttpClient = new();
	public const string ActivitySourceName = "CustomActivitySource";
	private static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");
	private static readonly Meter Meter = new("CustomMeter");
	private static readonly Counter<int> Counter = Meter.CreateCounter<int>("invocations",
		null, null, [KeyValuePair.Create<string, object?>("label1", "value1")]);

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_logger.LogInformation("Sending request... ");

		using (var activity = ActivitySource.StartActivity("DoingStuff", ActivityKind.Internal))
		{
			activity?.SetTag("CustomTag", "TagValue");
			
			if (Counter.Enabled)
				Counter.Add(1);

			_logger.LogInformation("Sending request... ");

			await Task.Delay(100, stoppingToken);
			var response = await HttpClient.GetAsync("http://elastic.co", stoppingToken);
			await Task.Delay(50, stoppingToken);

			if (response.StatusCode == System.Net.HttpStatusCode.OK)
				activity?.SetStatus(ActivityStatusCode.Ok);
			else
				activity?.SetStatus(ActivityStatusCode.Error);
		}

		await Task.Delay(5000, stoppingToken);
	}
}