// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Diagnostics;

namespace Elastic.OpenTelemetry.Tests.Diagnostics;

/// <summary>
/// Validates that the <see cref="CompositeLogger"/> deferral decision is decoupled from
/// <see cref="CompositeElasticOpenTelemetryOptions.IsOpAmpEnabled"/> and uses only
/// the presence of <see cref="CompositeElasticOpenTelemetryOptions.OpAmpEndpoint"/>.
/// </summary>
public class CompositeLoggerDeferralTests
{
	[Fact]
	public void DeferredMode_WhenOpAmpEndpointConfigured()
	{
		// Endpoint set but no ServiceName yet — logger should defer
		var options = new CompositeElasticOpenTelemetryOptions(new Dictionary<string, string>
		{
			["ELASTIC_OTEL_OPAMP_ENDPOINT"] = "http://localhost:4320"
		});

		var logger = new CompositeLogger(options);

		// In deferred mode, IsEnabled returns true for all levels (queuing)
		Assert.True(logger.IsEnabled(LogLevel.Trace));

		// Clean up
		logger.Activate(options);
		logger.Dispose();
	}

	[Fact]
	public void ActiveMode_WhenNoOpAmpEndpoint()
	{
		var options = new CompositeElasticOpenTelemetryOptions(new Dictionary<string, string>());

		var logger = new CompositeLogger(options);

		// Active mode — IsEnabled depends on sub-logger configuration, not unconditionally true.
		// With default options (no file logging, no additional logger), Trace should not be enabled.
		Assert.False(logger.IsEnabled(LogLevel.Trace));

		logger.Dispose();
	}

	[Fact]
	public void DeferredMode_WhenOpAmpConfiguredViaResourceAttributes()
	{
		// Endpoint set + service.name in OTEL_RESOURCE_ATTRIBUTES (but no explicit ServiceName)
		var options = new CompositeElasticOpenTelemetryOptions(new Dictionary<string, string>
		{
			["ELASTIC_OTEL_OPAMP_ENDPOINT"] = "http://localhost:4320",
			["OTEL_RESOURCE_ATTRIBUTES"] = "service.name=my-service"
		});

		var logger = new CompositeLogger(options);

		// Deferred mode — endpoint is present, so we defer regardless of ServiceName resolution
		Assert.True(logger.IsEnabled(LogLevel.Trace));

		// Clean up
		logger.Activate(options);
		logger.Dispose();
	}
}
