// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Core.Configuration;
using Elastic.OpenTelemetry.OpAmp.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Elastic.OpenTelemetry.Tests.Configuration;

/// <summary>
/// Tests for <see cref="CentralConfiguration"/>'s resilience across the full StartClient
/// lifecycle: success, cancellation, faults, synchronous throws, and ignore-cancellation paths.
///
/// <para><b>Test doubles:</b></para>
/// <list type="bullet">
/// <item><see cref="FaultingOpAmpClient"/> — configurable async faults/delays/cancellation</item>
/// <item><see cref="SyncThrowingOpAmpClient"/> — non-async StopAsync that throws synchronously</item>
/// </list>
///
/// <para><b>Threading model:</b> <c>CentralConfiguration</c>'s constructor is synchronous but
/// internally spawns a background thread. Most tests assert state after the constructor returns,
/// at which point <c>Thread.Join</c> has provided a happens-before guarantee. The
/// ignore-cancellation tests are different — <c>Join</c> times out and the worker runs
/// concurrently with the assertions, so those tests poll for completion.</para>
/// </summary>
public class CentralConfigurationResilienceTests
{
	private static readonly ILogger Logger = NullLoggerFactory.Instance.CreateLogger<CentralConfigurationResilienceTests>();

	// ── StartClient outcome tests ──
	// Each test targets a specific branch in StartClient. See the "Behaviour by Branch"
	// table in specs/HARDEN-opamp-startup-blocking-v2.md for the full matrix.

	[Fact]
	public void StartAsync_Faults_FallsBackToEmptyClient()
	{
		var client = new FaultingOpAmpClient(startException: new InvalidOperationException("connection refused"));

		var config = new CentralConfiguration(client, Logger);

		Assert.True(client.StartCalled);
		Assert.True(client.StopCalled);
		Assert.True(client.Disposed);
		Assert.False(config.WaitForFirstConfig(TimeSpan.FromMilliseconds(100)));

		config.Dispose();
	}

	[Fact]
	public void StartAsync_CancelledOnTimeout_FallsBackToEmptyClient()
	{
		var client = new FaultingOpAmpClient(startDelay: TimeSpan.FromSeconds(10));

		var sw = Stopwatch.StartNew();
		var config = new CentralConfiguration(client, Logger);
		sw.Stop();

		// On slow CI the constructor's Join may time out before the worker finishes its finally block.
		// Poll briefly — the worker should complete quickly once the CTS fires at ~2s.
		var deadline = DateTime.UtcNow.AddSeconds(5);
		while (DateTime.UtcNow < deadline && (!client.StopCalled || !client.Disposed))
			Thread.Sleep(50);

		Assert.True(client.StartCalled);
		Assert.True(client.StopCalled);
		Assert.True(client.Disposed);
		Assert.False(config.WaitForFirstConfig(TimeSpan.FromMilliseconds(100)));

		// Constructor must complete well under the 10s delay, proving CTS cancellation worked.
		// Expected: ~2s (CTS fires) + worker cleanup + scheduling margin ≈ 3s Join timeout.
		// The 4s bound gives 1s of CI headroom while still catching a startup budget regression.
		Assert.True(sw.Elapsed.TotalSeconds < 4,
			$"Constructor took {sw.Elapsed.TotalSeconds:F1}s — cancellation may not have fired");

		config.Dispose();
	}

	[Fact]
	public void StartAsync_Succeeds_ClientRetainedAndStartCompleted()
	{
		var client = new FaultingOpAmpClient();

		var config = new CentralConfiguration(client, Logger);

		Assert.True(client.StartCalled);
		Assert.NotNull(client.StartCompletedAt);
		Assert.Null(client.StopCalledAt); // Stop not called during successful start

		// Client retained — Dispose should stop and dispose it
		config.Dispose();
		Assert.True(client.StopCalled);
		Assert.True(client.Disposed);
	}

	[Fact]
	public void StartAsync_CancelledOnTimeout_StopCalledAfterStartUnwinds()
	{
		var client = new FaultingOpAmpClient(startDelay: TimeSpan.FromSeconds(10));

		var config = new CentralConfiguration(client, Logger);

		// On slow CI the constructor's Join may time out before the worker finishes its finally block.
		// Poll briefly — the worker should complete quickly once the CTS fires at ~2s.
		var deadline = DateTime.UtcNow.AddSeconds(5);
		while (DateTime.UtcNow < deadline && (!client.StartCompletedAt.HasValue || !client.StopCalledAt.HasValue))
			Thread.Sleep(50);

		Assert.True(client.StartCalled);
		Assert.NotNull(client.StartCompletedAt);
		Assert.NotNull(client.StopCalledAt);
		// Both timestamps are set on the worker thread, sequentially. Once the worker has
		// terminated (Join or poll), both are in their final state.
		// This proves StopAsync does not race with an in-flight StartAsync.
		Assert.True(client.StopCalledAt >= client.StartCompletedAt,
			$"StopAsync called at {client.StopCalledAt:O} before StartAsync completed at {client.StartCompletedAt:O}");

		config.Dispose();
	}

	[Fact]
	public void StartAsync_IgnoresCancellation_CallerFallsBackPromptly()
	{
		// Client ignores the CancellationToken — simulates a buggy upstream that doesn't
		// observe cancellation. The 5s delay exceeds both the CTS timeout (2s) and the
		// Join timeout (3s), so thread.Join returns false and the caller falls back.
		var client = new FaultingOpAmpClient(startDelay: TimeSpan.FromSeconds(5), ignoreCancellation: true);

		var sw = Stopwatch.StartNew();
		var config = new CentralConfiguration(client, Logger);
		sw.Stop();

		Assert.True(client.StartCalled);
		Assert.False(config.WaitForFirstConfig(TimeSpan.FromMilliseconds(100)));

		// Caller returned before the worker's cleanup ran — proves the caller doesn't
		// block on worker cleanup when Join times out.
		Assert.False(client.StopCalled, "Caller should not have waited for worker cleanup");
		Assert.False(client.Disposed, "Client should not be disposed yet — worker is still running");

		// Join timeout = 2000 + 500 + 500 = 3000ms. Constructor must complete well under
		// the 5s delay, proving the Join timeout fired. The 4s bound gives 1s of CI headroom.
		Assert.True(sw.Elapsed.TotalSeconds < 4,
			$"Constructor took {sw.Elapsed.TotalSeconds:F1}s — Join timeout may not be working");

		config.Dispose();
	}

	[Fact]
	public void StartAsync_IgnoresCancellation_EventuallyFaults_WorkerCleansUp()
	{
		// Client ignores cancellation AND eventually faults after the delay.
		// This exercises the "worker still alive at Join, eventually faults and runs cleanup" path.
		var client = new FaultingOpAmpClient(
			startDelay: TimeSpan.FromSeconds(5),
			ignoreCancellation: true,
			startException: new InvalidOperationException("late failure"));

		var sw = Stopwatch.StartNew();
		var config = new CentralConfiguration(client, Logger);
		sw.Stop();

		Assert.True(client.StartCalled);
		Assert.False(config.WaitForFirstConfig(TimeSpan.FromMilliseconds(100)));

		// Caller completes promptly via Join timeout
		Assert.True(sw.Elapsed.TotalSeconds < 4,
			$"Constructor took {sw.Elapsed.TotalSeconds:F1}s — Join timeout may not be working");

		// Wait for the worker to finish: 5s delay + cleanup margin.
		// Poll rather than Thread.Sleep to reduce flakiness on slow CI.
		var deadline = DateTime.UtcNow.AddSeconds(10);
		while (DateTime.UtcNow < deadline && (!client.StopCalled || !client.Disposed))
			Thread.Sleep(100);

		Assert.True(client.StopCalled, "Worker should have called StopAsync after faulting");
		Assert.True(client.Disposed, "Worker should have disposed client after faulting");

		config.Dispose();
	}

	[Fact]
	public void StartAsync_IgnoresCancellation_LateSuccess_ClientLeaked()
	{
		// Documents the accepted tradeoff: if StartAsync ignores the CancellationToken
		// AND eventually succeeds after the Join timeout, the worker sees outcome==Succeeded
		// and skips cleanup. But the caller already swapped to EmptyOpAmpClient — so nobody
		// calls StopAsync/Dispose on the successfully-started client. It is leaked.
		//
		// Closing this would require an atomic CAS handoff between caller and worker, which
		// adds meaningful complexity for a double-pathological case. The background thread
		// (IsBackground=true) terminates on process exit.
		// See "Known Tradeoff" in specs/HARDEN-opamp-startup-blocking-v2.md.
		var client = new FaultingOpAmpClient(
			startDelay: TimeSpan.FromSeconds(5),
			ignoreCancellation: true);

		var sw = Stopwatch.StartNew();
		var config = new CentralConfiguration(client, Logger);
		sw.Stop();

		// Caller fell back promptly
		Assert.True(sw.Elapsed.TotalSeconds < 4);
		Assert.False(config.WaitForFirstConfig(TimeSpan.FromMilliseconds(100)));

		// Wait for the worker to finish: it will succeed (no startException) after the 5s delay
		var deadline = DateTime.UtcNow.AddSeconds(10);
		while (DateTime.UtcNow < deadline && !client.StartCompletedAt.HasValue)
			Thread.Sleep(100);

		Assert.NotNull(client.StartCompletedAt);

		// Give a brief window for any cleanup to run (it shouldn't)
		Thread.Sleep(200);

		// This is the documented leak — the worker saw Succeeded and skipped cleanup
		Assert.False(client.StopCalled, "StopAsync should NOT be called — worker skips cleanup on success");
		Assert.False(client.Disposed, "Client should NOT be disposed — this is the accepted leak");

		config.Dispose();
	}

	// ── Worker cleanup fault tests ───────────────────────────────────────────────
	// These verify that faults during the worker's cleanup path (StopAsync, Dispose)
	// don't crash the background thread or prevent the constructor from returning.

	[Fact]
	public void StopAsync_Faults_DuringStartCleanup_ConstructorStillCompletes()
	{
		var client = new FaultingOpAmpClient(
			startDelay: TimeSpan.FromSeconds(10),
			stopException: new InvalidOperationException("stop failed"));

		var config = new CentralConfiguration(client, Logger);

		// On slow CI the constructor's Join may time out before the worker finishes its finally block.
		// Poll briefly — the worker should complete quickly once the CTS fires at ~2s.
		var deadline = DateTime.UtcNow.AddSeconds(5);
		while (DateTime.UtcNow < deadline && (!client.StopCalled || !client.Disposed))
			Thread.Sleep(50);

		Assert.True(client.StartCalled);
		Assert.True(client.StopCalled);
		Assert.True(client.Disposed);
		Assert.False(config.WaitForFirstConfig(TimeSpan.FromMilliseconds(100)));

		config.Dispose();
	}

	[Fact]
	public void StopAsync_ThrowsSynchronously_DuringStartCleanup_WorkerSurvives()
	{
		// SyncThrowingOpAmpClient.StopAsync is non-async and throws before returning a Task.
		// This exercises the try/catch around client.StopAsync() in the worker's finally that
		// prevents a synchronous throw from escaping the background thread.
		var client = new SyncThrowingOpAmpClient(startDelay: TimeSpan.FromSeconds(10));

		var config = new CentralConfiguration(client, Logger);

		// On slow CI the constructor's Join may time out before the worker finishes its finally block.
		// Poll briefly — the worker should complete quickly once the CTS fires at ~2s.
		var deadline = DateTime.UtcNow.AddSeconds(5);
		while (DateTime.UtcNow < deadline && !client.Disposed)
			Thread.Sleep(50);

		Assert.True(client.StartCalled);
		// SafeDispose must still run despite the synchronous StopAsync throw
		Assert.True(client.Disposed);
		Assert.False(config.WaitForFirstConfig(TimeSpan.FromMilliseconds(100)));

		config.Dispose();
	}

	// ── Normal-path Dispose fault tests ──────────────────────────────────────────
	// These test the Dispose/DisposeAsync path (not the worker's cleanup path).
	// The client started *successfully*, so _client still holds the real client.

	[Fact]
	public void StopAsync_Faults_DuringDispose_DoesNotThrow()
	{
		var client = new FaultingOpAmpClient(stopException: new InvalidOperationException("stop failed"));

		// Start succeeds, so client is retained (not swapped to EmptyOpAmpClient)
		var config = new CentralConfiguration(client, Logger);

		Assert.True(client.StartCalled);

		// Dispose should not throw even though StopAsync faults
		var exception = Record.Exception(() => config.Dispose());
		Assert.Null(exception);
		Assert.True(client.Disposed);
	}

	// ── Config subscriber and happy-path tests ──

	[Fact]
	public void WaitForFirstConfig_TimesOut_ReturnsFalse()
	{
		// Client starts successfully but never sends config
		var client = new FaultingOpAmpClient();

		var config = new CentralConfiguration(client, Logger);

		Assert.True(client.StartCalled);
		Assert.False(config.WaitForFirstConfig(TimeSpan.FromMilliseconds(100)));

		config.Dispose();
	}

	[Fact]
	public void HappyPath_ClientRetained()
	{
		var client = new FaultingOpAmpClient();

		var config = new CentralConfiguration(client, Logger);

		Assert.True(client.StartCalled);

		// Client was not replaced — Dispose should stop and dispose it
		config.Dispose();
		Assert.True(client.StopCalled);
		Assert.True(client.Disposed);
	}

	[Fact]
	public void TryGetInitialConfig_BeforeAnyMessage_ReturnsFalse()
	{
		var client = new FaultingOpAmpClient();
		var config = new CentralConfiguration(client, Logger);

		Assert.False(config.TryGetInitialConfig(out var initial));
		Assert.Null(initial);

		config.Dispose();
	}

	[Fact]
	public async Task HandleMessage_PublishesInitialConfig()
	{
		var client = new FaultingOpAmpClient();
		var config = new CentralConfiguration(client, Logger);
		var subscriber = client.CapturedSubscriber!;

		var remoteConfig = new ElasticRemoteConfig("Information");

		// Deliver config from a background thread to exercise cross-thread visibility
		await Task.Run(() => subscriber.HandleMessage(remoteConfig));

		Assert.True(config.TryGetInitialConfig(out var initial));
		Assert.Same(remoteConfig, initial);

		config.Dispose();
	}

	[Fact]
	public async Task HandleMessage_SecondCall_DoesNotOverwriteInitialConfig()
	{
		var client = new FaultingOpAmpClient();
		var config = new CentralConfiguration(client, Logger);
		var subscriber = client.CapturedSubscriber!;

		var first = new ElasticRemoteConfig("Information");
		var second = new ElasticRemoteConfig("Debug");

		subscriber.HandleMessage(first);

		// Second call from a different thread — first writer should still win
		await Task.Run(() => subscriber.HandleMessage(second));

		Assert.True(config.TryGetInitialConfig(out var initial));
		Assert.Same(first, initial);

		config.Dispose();
	}

	// ── Exactly-once dispose tests ──
	// The Interlocked.Exchange guard on _disposed must prevent double-stop and
	// double-dispose regardless of call ordering, concurrency, or prior faults.

	[Fact]
	public void Dispose_CalledTwice_DoesNotDoubleDispose()
	{
		var client = new FaultingOpAmpClient();
		var config = new CentralConfiguration(client, Logger);

		config.Dispose();
		config.Dispose();

		Assert.Equal(1, client.StopCount);
		Assert.Equal(1, client.DisposeCount);
	}

	[Fact]
	public async Task DisposeAsync_CalledTwice_DoesNotDoubleDispose()
	{
		var client = new FaultingOpAmpClient();
		var config = new CentralConfiguration(client, Logger);

		await config.DisposeAsync();
		await config.DisposeAsync();

		Assert.Equal(1, client.StopCount);
		Assert.Equal(1, client.DisposeCount);
	}

	[Fact]
	public async Task Dispose_And_DisposeAsync_Concurrent_DoesNotDoubleDispose()
	{
		var client = new FaultingOpAmpClient();
		var config = new CentralConfiguration(client, Logger);

		var barrier = new Barrier(2);
		var t1 = Task.Run(() => { barrier.SignalAndWait(); config.Dispose(); });
		var t2 = Task.Run(() => { barrier.SignalAndWait(); return config.DisposeAsync().AsTask(); });
		await Task.WhenAll(t1, t2);

		Assert.Equal(1, client.StopCount);
		Assert.Equal(1, client.DisposeCount);
	}

	[Fact]
	public void Dispose_FirstCallFaults_SecondCallStillBailsOut()
	{
		var client = new FaultingOpAmpClient(stopException: new InvalidOperationException("stop failed"));
		var config = new CentralConfiguration(client, Logger);

		// First dispose — StopAsync faults, but Interlocked.Exchange already flipped _disposed to 1
		config.Dispose();

		// Second dispose — should bail out immediately, not attempt stop/dispose again
		config.Dispose();

		Assert.Equal(1, client.StopCount);
		Assert.Equal(1, client.DisposeCount);
	}

	[Fact]
	public async Task Dispose_ThenDisposeAsync_SecondCallIsNoOp()
	{
		var client = new FaultingOpAmpClient();
		var config = new CentralConfiguration(client, Logger);

		config.Dispose();
		await config.DisposeAsync();

		Assert.Equal(1, client.StopCount);
		Assert.Equal(1, client.DisposeCount);
	}

	[Fact]
	public async Task DisposeAsync_ThenDispose_SecondCallIsNoOp()
	{
		var client = new FaultingOpAmpClient();
		var config = new CentralConfiguration(client, Logger);

		await config.DisposeAsync();
		config.Dispose();

		Assert.Equal(1, client.StopCount);
		Assert.Equal(1, client.DisposeCount);
	}

	[Fact]
	public async Task DisposeAsync_StopFaults_DoesNotThrow()
	{
		var client = new FaultingOpAmpClient(stopException: new InvalidOperationException("stop failed"));
		var config = new CentralConfiguration(client, Logger);

		var exception = await Record.ExceptionAsync(async () => await config.DisposeAsync());
		Assert.Null(exception);
		Assert.True(client.Disposed);
	}

	[Fact]
	public async Task DisposeAsync_StopTimesOut_DoesNotThrow()
	{
		var client = new FaultingOpAmpClient(stopDelay: TimeSpan.FromSeconds(10));
		var config = new CentralConfiguration(client, Logger);

		var exception = await Record.ExceptionAsync(async () => await config.DisposeAsync());
		Assert.Null(exception);
		Assert.True(client.Disposed);
	}

	[Fact]
	public async Task EmptyOpAmpClient_RepeatedDispose_DoesNotThrow()
	{
		var client = new EmptyOpAmpClient();
		var config = new CentralConfiguration(client, Logger);

		config.Dispose();
		config.Dispose();
		await config.DisposeAsync();

		// No assertions on counters — EmptyOpAmpClient short-circuits before
		// StopAsync or SafeDispose are reached. We're just verifying no exceptions.
	}
}
