// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Diagnostics;

namespace Elastic.OpenTelemetry.Tests.Diagnostics;

/// <summary>
/// Validates the two paths of the <see cref="CompositeLogger"/> safety timer callback:
/// activate with options, or discard the queue. Tests call <see cref="CompositeLogger.OnSafetyTimerElapsed"/>
/// directly — zero timing dependencies, deterministic, runs in microseconds.
/// </summary>
public class CompositeLoggerSafetyTimerTests
{
	private static (CompositeLogger logger, CountingLogger sink, CompositeElasticOpenTelemetryOptions options) CreateDeferredLogger()
	{
		var sink = new CountingLogger();
		var env = new Dictionary<string, string>
		{
			["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://localhost:4317",
			["ELASTIC_OTEL_OPAMP_ENDPOINT"] = "http://localhost:4320",
			["OTEL_SERVICE_NAME"] = "test-service",
		};
		var options = new CompositeElasticOpenTelemetryOptions(env)
		{
			AdditionalLogger = sink
		};
		var logger = new CompositeLogger(options);
		return (logger, sink, options);
	}

	[Fact]
	public void SafetyTimer_WithOptions_ActivatesLogger()
	{
		var (logger, sink, _) = CreateDeferredLogger();

		try
		{
			// Log 5 messages — queued in deferred mode
			for (var i = 0; i < 5; i++)
				logger.Log(LogLevel.Information, new EventId(i), $"pre-timer {i}", null, (s, _) => s);

			// Safety timer fires — should activate with options
			logger.OnSafetyTimerElapsed();

			// Log 5 more — routed directly to sub-loggers
			for (var i = 5; i < 10; i++)
				logger.Log(LogLevel.Information, new EventId(i), $"post-timer {i}", null, (s, _) => s);

			// 12 total: 1 init debug (queued in ctor) + 5 queued = 6 drained + 1 CompositeLoggerActivated + 5 direct
			Assert.Equal(12, sink.Count);
		}
		finally
		{
			logger.Dispose();
		}
	}

	[Fact]
	public void SafetyTimer_WithoutOptions_DiscardsQueue()
	{
		var logger = new CompositeLogger(options: null);

		try
		{
			// In deferred mode, IsEnabled returns true so messages are queued
			Assert.True(logger.IsEnabled(LogLevel.Information));

			// Log 5 messages — queued
			for (var i = 0; i < 5; i++)
				logger.Log(LogLevel.Information, new EventId(i), $"pre-timer {i}", null, (s, _) => s);

			// Safety timer fires — no options, discards queue
			logger.OnSafetyTimerElapsed();

			// After discard, IsEnabled returns false (no sub-loggers were created)
			Assert.False(logger.IsEnabled(LogLevel.Information));

			// Log 5 more — silently dropped (no sub-loggers exist to receive them)
			for (var i = 5; i < 10; i++)
				logger.Log(LogLevel.Information, new EventId(i), $"post-timer {i}", null, (s, _) => s);

			// IsEnabled remains false — the logger is a no-op after discard
			Assert.False(logger.IsEnabled(LogLevel.Information));
		}
		finally
		{
			logger.Dispose();
		}
	}

	[Fact]
	public void SafetyTimer_DoesNotDoubleActivate_AfterExplicitActivation()
	{
		var (logger, sink, options) = CreateDeferredLogger();

		try
		{
			// Explicitly activate
			logger.Activate(options);

			// Log 3 messages — routed directly
			for (var i = 0; i < 3; i++)
				logger.Log(LogLevel.Information, new EventId(i), $"post-activate {i}", null, (s, _) => s);

			// 5 total: 1 init debug drained + 1 CompositeLoggerActivated + 3 direct
			Assert.Equal(5, sink.Count);

			// Safety timer fires — should be a no-op (already activated, queue is null)
			logger.OnSafetyTimerElapsed();

			// Log 3 more — still routed normally
			for (var i = 3; i < 6; i++)
				logger.Log(LogLevel.Information, new EventId(i), $"post-timer {i}", null, (s, _) => s);

			// 8 total: 5 from first assert + 3 more direct
			Assert.Equal(8, sink.Count);
		}
		finally
		{
			logger.Dispose();
		}
	}

	[Fact]
	public void SafetyTimer_OptionsArriveViaGetOrCreate_ActivatesLogger()
	{
		CompositeLogger? logger = null;

		try
		{
			// Step 1: Create deferred logger with no options via the singleton path
			logger = CompositeLogger.GetOrCreate(options: null);

			// Step 2: Log messages while the instance has no options (queued)
			for (var i = 0; i < 3; i++)
				logger.Log(LogLevel.Information, new EventId(i), $"pre-options {i}", null, (s, _) => s);

			// Step 3: Create OpAmp-enabled options with a counting sink
			var sink = new CountingLogger();
			var env = new Dictionary<string, string>
			{
				["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://localhost:4317",
				["ELASTIC_OTEL_OPAMP_ENDPOINT"] = "http://localhost:4320",
				["OTEL_SERVICE_NAME"] = "test-service",
			};
			var options = new CompositeElasticOpenTelemetryOptions(env)
			{
				AdditionalLogger = sink
			};

			// Step 4: GetOrCreate with options — stores options on existing instance
			var sameLogger = CompositeLogger.GetOrCreate(options);
			Assert.Same(logger, sameLogger);

			// Step 5: Safety timer fires — should activate with the late-arriving options
			logger.OnSafetyTimerElapsed();

			// Step 6: Log messages after activation — routed directly
			for (var i = 3; i < 8; i++)
				logger.Log(LogLevel.Information, new EventId(i), $"post-timer {i}", null, (s, _) => s);

			// 10 total: 1 init debug (queued in ctor) + 3 queued = 4 drained + 1 CompositeLoggerActivated + 5 direct
			Assert.Equal(10, sink.Count);
		}
		finally
		{
			logger?.Dispose();
			CompositeLogger.ClearPreActivationInstance();
		}
	}
}
