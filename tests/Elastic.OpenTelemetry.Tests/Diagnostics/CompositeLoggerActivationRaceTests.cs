// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Diagnostics;

namespace Elastic.OpenTelemetry.Tests.Diagnostics;

/// <summary>
/// Validates that the deferred-to-active handoff in <see cref="CompositeLogger"/> does not
/// lose log events when concurrent threads are enqueuing while activation is in progress.
/// Regression tests for CR-004.
/// Tests that exercise <see cref="CompositeLogger.GetOrCreate"/> are grouped into this
/// collection to prevent parallel access to the static <c>PreActivationInstance</c> singleton.
/// </summary>
[Collection("CompositeLoggerSingleton")]
public class CompositeLoggerActivationRaceTests
{
	/// <summary>
	/// Stress test: many threads log concurrently while activation runs mid-stream.
	/// Every event must be observed by the additional logger — none may be silently dropped.
	/// </summary>
	[Fact]
	public void ConcurrentLogging_DuringActivation_DoesNotLoseEvents()
	{
		const int threadCount = 16;
		const int messagesPerThread = 500;
		var expectedTotal = threadCount * messagesPerThread;

		var sink = new CountingLogger();

		// Create options that will activate with our counting sink.
		// Use the IDictionary constructor to avoid reading real env vars,
		// and set OpAmp properties so the logger enters deferred mode.
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

		// Verify we're in deferred mode (CompositeLogger defers when OpAmpEndpoint is set)
		Assert.False(string.IsNullOrEmpty(options.OpAmpEndpoint),
			"Options should have OpAmpEndpoint set for this test to exercise deferred mode.");

		var logger = new CompositeLogger(options);

		using var barrier = new ManualResetEventSlim(false);

		// Launch producer threads that will log messagesPerThread each
		var threads = new Thread[threadCount];
		for (var t = 0; t < threadCount; t++)
		{
			var threadIndex = t;
			threads[t] = new Thread(() =>
			{
				barrier.Wait();
				for (var i = 0; i < messagesPerThread; i++)
				{
					logger.Log(
						LogLevel.Information,
						new EventId(threadIndex * messagesPerThread + i),
						$"Thread {threadIndex} message {i}",
						null,
						(s, _) => s);
				}
			})
			{
				IsBackground = true
			};
			threads[t].Start();
		}

		// Release all threads, then activate after a tiny delay to maximize the race window
		barrier.Set();
		Thread.SpinWait(100);
		logger.Activate(options);

		// Wait for all producer threads to complete
		foreach (var t in threads)
			t.Join(10_000);

		// +2 for the init debug msg queued in the ctor and the CompositeLoggerActivated msg after drain
		Assert.Equal(expectedTotal + 2, sink.Count);
	}

	/// <summary>
	/// Verifies the post-activation isolation invariant: once the pre-activation singleton is
	/// activated, a subsequent <see cref="CompositeLogger.GetOrCreate"/> call — even with
	/// identical options — must return a fresh independent instance rather than the already-
	/// activated logger. This is the behavioral guarantee behind the <c>IsCompatible</c>
	/// <c>_activationState</c> guard: without that guard, a concurrent caller that captures
	/// <c>PreActivationInstance</c> before <c>Activate</c> clears it would observe
	/// <c>_options == null</c> (post-activation) and incorrectly conclude the logger was still
	/// in a compatible pre-activation state.
	/// </summary>
	[Fact]
	public void GetOrCreate_AfterActivation_ReturnsFreshInstance()
	{
		var sink = new CountingLogger();
		var env = new Dictionary<string, string>
		{
			["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://localhost:4317",
			["ELASTIC_OTEL_OPAMP_ENDPOINT"] = "http://localhost:4320",
			["OTEL_SERVICE_NAME"] = "test-service",
		};
		var options = new CompositeElasticOpenTelemetryOptions(env) { AdditionalLogger = sink };

		CompositeLogger? first = null;
		CompositeLogger? second = null;

		try
		{
			first = CompositeLogger.GetOrCreate(options);
			first.Activate(options);

			second = CompositeLogger.GetOrCreate(options);

			Assert.NotSame(first, second);
		}
		finally
		{
			first?.Dispose();
			second?.Dispose();
			CompositeLogger.ClearPreActivationInstance();
		}
	}

	/// <summary>
	/// Validates the late-options-arrival path: a logger created without options via
	/// <see cref="CompositeLogger.GetOrCreate"/> receives options on a second call,
	/// and when the safety timer fires it activates using those late-arriving options.
	/// Placed here (rather than SafetyTimerTests) because it exercises the static
	/// <c>PreActivationInstance</c> singleton and requires collection-level isolation.
	/// </summary>
	[Fact]
	public void SafetyTimer_OptionsArriveViaGetOrCreate_ActivatesLogger()
	{
		CompositeLogger? logger = null;

		// Ensure no stale singleton from a prior test in the collection can interfere
		// with the GetOrCreate singleton behaviour this test exercises.
		CompositeLogger.ClearPreActivationInstance();

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

	/// <summary>
	/// Focused test that repeatedly creates loggers, logs from many threads, activates,
	/// and verifies no events are lost — over many iterations to maximize the chance of
	/// hitting the handoff window.
	/// </summary>
	[Fact]
	public void RepeatedActivationDuringConcurrentLogging_NeverLosesEvents()
	{
		const int iterations = 50;
		const int threadCount = 8;
		const int messagesPerThread = 100;
		var expectedTotal = threadCount * messagesPerThread;

		for (var iter = 0; iter < iterations; iter++)
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

			using var barrier = new ManualResetEventSlim(false);
			var threads = new Thread[threadCount];

			for (var t = 0; t < threadCount; t++)
			{
				var threadIndex = t;
				threads[t] = new Thread(() =>
				{
					barrier.Wait();
					for (var i = 0; i < messagesPerThread; i++)
					{
						logger.Log(
							LogLevel.Information,
							new EventId(threadIndex * messagesPerThread + i),
							$"iter {iter} thread {threadIndex} msg {i}",
							null,
							(s, _) => s);
					}
				})
				{
					IsBackground = true
				};
				threads[t].Start();
			}

			// Fire all threads then immediately activate to maximize the race
			barrier.Set();
			logger.Activate(options);

			foreach (var t in threads)
				t.Join(10_000);

			// +2 for the init debug msg queued in the ctor and the CompositeLoggerActivated msg after drain
			Assert.True(
				sink.Count == expectedTotal + 2,
				$"Iteration {iter}: expected {expectedTotal + 2} events but got {sink.Count}. " +
				$"Lost {expectedTotal + 2 - sink.Count} events during activation handoff.");
		}
	}

}
