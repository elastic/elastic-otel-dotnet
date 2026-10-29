// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Core;
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
	public void SafetyTimer_DuringBootstrap_DoesNotActivate_UntilBootstrapActivates()
	{
		var (logger, sink, options) = CreateDeferredLogger();

		try
		{
			logger.EnterBootstrap();

			for (var i = 0; i < 5; i++)
				logger.Log(LogLevel.Information, new EventId(i), $"pre-timer {i}", null, (s, _) => s);

			// Safety timer fires while bootstrap is still waiting for central config — must not activate
			logger.OnSafetyTimerElapsed();
			Assert.Equal(0, sink.Count);

			// Bootstrap completes normally and activates with the final options
			logger.Activate(options);
			logger.ExitBootstrap();

			// 7 total: 1 init debug + 5 queued = 6 drained + 1 CompositeLoggerActivated
			Assert.Equal(7, sink.Count);

			logger.Log(LogLevel.Information, new EventId(5), "post-bootstrap", null, (s, _) => s);
			Assert.Equal(8, sink.Count);
		}
		finally
		{
			logger.Dispose();
		}
	}

	[Fact]
	public void SafetyTimer_DuringBootstrap_ActivatesOnExit_WhenBootstrapDidNotActivate()
	{
		var (logger, sink, _) = CreateDeferredLogger();

		try
		{
			logger.EnterBootstrap();

			for (var i = 0; i < 5; i++)
				logger.Log(LogLevel.Information, new EventId(i), $"pre-timer {i}", null, (s, _) => s);

			logger.OnSafetyTimerElapsed();
			Assert.Equal(0, sink.Count);

			// Bootstrap fails before reaching Activate — exiting runs the deferred safety timer logic
			logger.ExitBootstrap();

			// 7 total: 1 init debug + 5 queued = 6 drained + 1 CompositeLoggerActivated
			Assert.Equal(7, sink.Count);
		}
		finally
		{
			logger.Dispose();
		}
	}

	[Fact]
	public void ExitBootstrap_WithoutSafetyTimerFiring_DoesNotActivate()
	{
		var (logger, sink, _) = CreateDeferredLogger();

		try
		{
			logger.EnterBootstrap();
			logger.ExitBootstrap();

			// Nothing deferred, so the logger remains in deferred mode until the timer fires
			Assert.Equal(0, sink.Count);

			logger.OnSafetyTimerElapsed();

			// 2 total: 1 init debug drained + 1 CompositeLoggerActivated
			Assert.Equal(2, sink.Count);
		}
		finally
		{
			logger.Dispose();
		}
	}

	[Fact]
	public void SafetyTimerMs_ExceedsWorstCaseBootstrapWait()
	{
		var worstCaseBootstrapWaitMs = ElasticOpenTelemetry.OpAmpStartJoinTimeoutMs + ElasticOpenTelemetry.WaitForFirstConfigTimeoutMs;

		Assert.True(ElasticOpenTelemetry.SafetyTimerMs > worstCaseBootstrapWaitMs,
			$"SafetyTimerMs ({ElasticOpenTelemetry.SafetyTimerMs}) must exceed the worst-case bootstrap wait ({worstCaseBootstrapWaitMs}).");
	}
}
