// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Elastic.OpenTelemetry.Core;

internal sealed class ElasticOpenTelemetryComponents : IDisposable, IAsyncDisposable
{
	internal Guid InstanceId { get; } = Guid.NewGuid();

	public CompositeLogger Logger { get; }
	public LoggingEventListener LoggingEventListener { get; }
	public CompositeElasticOpenTelemetryOptions Options { get; }
	public CentralConfiguration? CentralConfiguration { get; }

	public ElasticOpenTelemetryComponents(
		CompositeElasticOpenTelemetryOptions options,
		CompositeLogger logger,
		LoggingEventListener loggingEventListener,
		CentralConfiguration? centralConfiguration)
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
		Logger.LogInformation("Disposing ElasticOpenTelemetryComponents instance '{InstanceId}'.", InstanceId);

		Logger.Dispose();
		LoggingEventListener.Dispose();
		CentralConfiguration?.Dispose();
	}

	public async ValueTask DisposeAsync()
	{
		Logger.LogInformation("Disposing ElasticOpenTelemetryComponents instance '{InstanceId}'.", InstanceId);

		await Logger.DisposeAsync().ConfigureAwait(false);
		await LoggingEventListener.DisposeAsync().ConfigureAwait(false);

		if (CentralConfiguration is not null)
			await CentralConfiguration.DisposeAsync().ConfigureAwait(false);
	}
}
