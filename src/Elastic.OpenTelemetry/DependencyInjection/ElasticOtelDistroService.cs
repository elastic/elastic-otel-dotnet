// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry
{
	internal sealed class ElasticOtelDistroService(IServiceProvider serviceProvider) : IHostedService, IHostedLifecycleService
	{
		private IAgent? _agent;

		public Task StartingAsync(CancellationToken cancellationToken)
		{
			var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
			var logger = loggerFactory?.CreateLogger($"{nameof(Elastic)}.{nameof(OpenTelemetry)}");

			_agent = serviceProvider.GetRequiredService<AgentBuilder>().Build(logger, serviceProvider);

			//logger.LogInformation("Initialising Agent.Current.");
			//Agent.SetAgent(_agent, logger);

			return Task.CompletedTask;
		}

		public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
		public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
		public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
		public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

		public async Task StoppedAsync(CancellationToken cancellationToken)
		{
			if (_agent != null)
				await _agent.DisposeAsync().ConfigureAwait(false);
		}
	}
}
