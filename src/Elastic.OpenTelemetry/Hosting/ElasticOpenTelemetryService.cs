// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.Hosting;

internal sealed class ElasticOpenTelemetryService(IServiceProvider serviceProvider) : IHostedLifecycleService
{
	private ElasticOpenTelemetryComponents? _components;

	public Task StartingAsync(CancellationToken cancellationToken)
	{
		var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
		var logger = loggerFactory?.CreateElasticLogger();

		var bootstrapInfo = serviceProvider.GetService<BootstrapInfo>();

		_components = serviceProvider.GetService<ElasticOpenTelemetryComponents>();
		
		if (bootstrapInfo is not null && bootstrapInfo.Success && _components is not null && logger is not null)
		{
			_components.SetAdditionalLogger(logger, bootstrapInfo.ActivationMethod);
		}

		return Task.CompletedTask;
	}

	public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	public async Task StoppedAsync(CancellationToken cancellationToken)
	{
		if (_components?.Logger is not null)
			await _components.Logger.DisposeAsync().ConfigureAwait(false);

		if (_components?.LoggingEventListener is not null)
			await _components.LoggingEventListener.DisposeAsync().ConfigureAwait(false);
	}
}
