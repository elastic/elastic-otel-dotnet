// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Diagnostics.Logging;
using Elastic.OpenTelemetry.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.Hosting;

internal sealed class ElasticOpenTelemetryService(IServiceProvider serviceProvider) : IHostedLifecycleService
{
	private IInstrumentationLifetime? _lifeTime;

	public Task StartingAsync(CancellationToken cancellationToken)
	{
		var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
		var logger = loggerFactory?.CreateLogger(CompositeLogger.LogCategory);

		_lifeTime = serviceProvider.GetRequiredService<ElasticOpenTelemetryBuilder>().Build(logger, serviceProvider);

		return Task.CompletedTask;
	}

	public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
	public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
	public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	public async Task StoppedAsync(CancellationToken cancellationToken)
	{
		if (_lifeTime != null)
			await _lifeTime.DisposeAsync().ConfigureAwait(false);
	}
}
