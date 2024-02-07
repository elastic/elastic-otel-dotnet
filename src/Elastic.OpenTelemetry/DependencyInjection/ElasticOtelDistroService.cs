// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information
using Elastic.OpenTelemetry.Processors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Elastic.OpenTelemetry
{
	internal sealed class ElasticOtelDistroService(IServiceProvider serviceProvider) : IHostedService, IHostedLifecycleService
	{
		private readonly IServiceProvider _serviceProvider = serviceProvider;

		public Task StartingAsync(CancellationToken cancellationToken)
		{
			var agent = _serviceProvider.GetRequiredService<IAgent>();
			var loggerFactory = _serviceProvider.GetService<ILoggerFactory>();
			var logger = loggerFactory?.CreateLogger($"{nameof(Elastic)}.{nameof(OpenTelemetry)}") ?? NullLogger.Instance;

			logger.LogInformation("Initialising processors.");
			foreach (var processor in _serviceProvider.GetServices<IElasticProcessor>())
				processor?.Initialize(_serviceProvider);

			logger.LogInformation("Initialising Agent.Current.");
			Agent.SetAgent(agent, logger);

			return Task.CompletedTask;
		}

		public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
		public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
		public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
		public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
		public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
	}
}
