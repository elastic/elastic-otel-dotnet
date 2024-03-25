// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Diagnostics;
using Elastic.OpenTelemetry.Diagnostics.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Elastic.OpenTelemetry;

internal class ElasticOpenTelemetry(
	CompositeLogger logger,
	LoggingEventListener loggingEventListener,
	TracerProvider tracerProvider,
	MeterProvider meterProvider
) : IElasticOpenTelemetry
{
	public void Dispose()
	{
		tracerProvider.Dispose();
		meterProvider.Dispose();
		loggingEventListener.Dispose();
		logger.Dispose();
	}

	public async ValueTask DisposeAsync()
	{
		tracerProvider.Dispose();
		meterProvider.Dispose();
		await loggingEventListener.DisposeAsync().ConfigureAwait(false);
		await logger.DisposeAsync().ConfigureAwait(false);
	}
}
