// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Elastic.OpenTelemetry.Core;

internal sealed class ElasticOpenTelemetryComponents(
	BootstrapInfo bootstrapInfo,
	CompositeLogger logger,
	LoggingEventListener loggingEventListener,
	CompositeElasticOpenTelemetryOptions options) : IDisposable, IAsyncDisposable
{
	public CompositeLogger Logger { get; } = logger;
	public LoggingEventListener LoggingEventListener { get; } = loggingEventListener;
	public CompositeElasticOpenTelemetryOptions Options { get; } = options;
	public BootstrapInfo BootstrapInfo { get; } = bootstrapInfo;

	internal void SetAdditionalLogger(ILogger? logger, SdkActivationMethod activationMethod)
	{
		if (logger is not null && logger is not NullLogger)
			Logger.SetAdditionalLogger(logger, activationMethod, this);
	}

	public void Dispose()
	{
		Logger.Dispose();
		LoggingEventListener.Dispose();
	}

	public async ValueTask DisposeAsync()
	{
		await Logger.DisposeAsync().ConfigureAwait(false);
		await LoggingEventListener.DisposeAsync().ConfigureAwait(false);
	}
}
