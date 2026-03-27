// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Core.Configuration;
using Elastic.OpenTelemetry.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Elastic.OpenTelemetry.Core;

internal sealed class ElasticOpenTelemetryComponents : IDisposable, IAsyncDisposable
{
	private int _disposed;

	internal Guid InstanceId { get; } = Guid.NewGuid();

	internal CompositeLogger Logger { get; }
	internal LoggingEventListener LoggingEventListener { get; }
	internal CompositeElasticOpenTelemetryOptions Options { get; }
	internal CentralConfiguration? CentralConfiguration { get; }

	public ElasticOpenTelemetryComponents(
		CompositeLogger logger,
		LoggingEventListener loggingEventListener,
		CompositeElasticOpenTelemetryOptions options,
		CentralConfiguration? centralConfiguration = null)
	{
		if (BootstrapLogger.IsEnabled)
		{
			BootstrapLogger.LogWithStackTrace($"{nameof(ElasticOpenTelemetryComponents)}: Instance '{InstanceId}' created via ctor." +
				$"{Environment.NewLine}    Invoked with `{nameof(CompositeLogger)}` instance '{logger.InstanceId}'." +
				$"{Environment.NewLine}    Invoked with `{nameof(OpenTelemetry.Diagnostics.LoggingEventListener)}` instance '{loggingEventListener.InstanceId}'." +
				$"{Environment.NewLine}    Invoked with `{nameof(CompositeElasticOpenTelemetryOptions)}` instance '{options.InstanceId}'.");
		}

		Logger = logger;
		LoggingEventListener = loggingEventListener;
		Options = options;
		CentralConfiguration = centralConfiguration;
	}

	internal void SetAdditionalLogger(ILogger logger, SdkActivationMethod activationMethod)
	{
		if (logger is not NullLogger)
		{
			if (BootstrapLogger.IsEnabled)
				BootstrapLogger.Log($"{nameof(ElasticOpenTelemetryComponents)}: Setting additional logger.");

			Logger.SetAdditionalLogger(logger, activationMethod, this);
		}
	}

	public void Dispose()
	{
		if (Interlocked.Exchange(ref _disposed, 1) != 0)
			return;

		using (ElasticOpenTelemetry.Lock.EnterScope())
			ElasticOpenTelemetry.SharedComponents.Remove(this);

		CentralConfiguration?.Dispose();
		Logger.Dispose();
		LoggingEventListener.Dispose();
	}

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _disposed, 1) != 0)
			return;

		using (ElasticOpenTelemetry.Lock.EnterScope())
			ElasticOpenTelemetry.SharedComponents.Remove(this);

		if (CentralConfiguration is not null)
			await CentralConfiguration.DisposeAsync().ConfigureAwait(false);
		await Logger.DisposeAsync().ConfigureAwait(false);
		await LoggingEventListener.DisposeAsync().ConfigureAwait(false);
	}
}
