// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information
using System.Diagnostics;

namespace Example.Elastic.OpenTelemetry.Worker
{
	public class Worker : BackgroundService
	{
		private readonly ILogger<Worker> _logger;

		private static readonly HttpClient HttpClient = new();
		private const string ActivitySourceName = "CustomActivitySource";
		private static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");

		public Worker(ILogger<Worker> logger) => _logger = logger;

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				_logger.LogInformation("Sending request... ");

				using (var activity = ActivitySource.StartActivity("DoingStuff", ActivityKind.Internal))
				{
					activity?.SetTag("CustomTag", "TagValue");

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
	}
}
