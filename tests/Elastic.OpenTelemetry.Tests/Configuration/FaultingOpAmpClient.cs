// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.OpAmp.Abstractions;

namespace Elastic.OpenTelemetry.Tests.Configuration;

/// <summary>
/// Configurable test double for <see cref="IOpAmpClient"/>. Each constructor parameter
/// injects a specific fault or timing behaviour into the corresponding lifecycle method,
/// letting tests exercise every branch of <c>CentralConfiguration.StartClient</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Thread-safety model:</b> <c>StartAsync</c> and <c>StopAsync</c> run on the worker
/// thread; test assertions run on the xUnit thread after <c>Thread.Join</c>. See the
/// comment on <see cref="StartCalled"/> for which properties need barriers and why.
/// </para>
/// <para>
/// <b>Async vs sync throws:</b> Both <see cref="StartAsync"/> and <see cref="StopAsync"/>
/// are <c>async</c> methods, so the C# state machine captures all exceptions into the
/// returned <see cref="Task"/> — they never throw synchronously. To test the synchronous-
/// throw path in <c>CentralConfiguration</c>, use <see cref="SyncThrowingOpAmpClient"/>
/// instead.
/// </para>
/// </remarks>
internal sealed class FaultingOpAmpClient : IOpAmpClient
{
	private readonly Exception? _startException;
	private readonly Exception? _stopException;
	private readonly TimeSpan? _startDelay;
	private readonly TimeSpan? _stopDelay;
	private readonly bool _ignoreCancellation;

	private int _stopCount;
	private int _disposeCount;

	// StartCalled and StartCompletedAt are plain properties — they are written at the very
	// start of StartAsync (on the worker thread) and read only after Thread.Join returns,
	// which provides a happens-before guarantee. When Join times out the properties have been
	// visible for seconds on any real architecture, so Volatile is unnecessary noise here.
	// StopCount/DisposeCount use Interlocked because they are also read in concurrent
	// Dispose tests where no Join gate exists.
	public bool StartCalled { get; private set; }
	public bool StopCalled => _stopCount > 0;
	public bool Disposed => _disposeCount > 0;

	public int StopCount => _stopCount;
	public int DisposeCount => _disposeCount;

	public DateTime? StartCompletedAt { get; private set; }
	public DateTime? StopCalledAt { get; private set; }

	public FaultingOpAmpClient(
		Exception? startException = null,
		Exception? stopException = null,
		TimeSpan? startDelay = null,
		TimeSpan? stopDelay = null,
		bool ignoreCancellation = false)
	{
		_startException = startException;
		_stopException = stopException;
		_startDelay = startDelay;
		_stopDelay = stopDelay;
		_ignoreCancellation = ignoreCancellation;
	}

	public async Task StartAsync(CancellationToken cancellationToken = default)
	{
		StartCalled = true;

		try
		{
			if (_startDelay.HasValue)
			{
				// When _ignoreCancellation is true, Task.Delay runs without the token.
				// This simulates a buggy upstream that doesn't observe CancellationToken,
				// forcing CentralConfiguration's Join timeout to fire instead.
				if (_ignoreCancellation)
					await Task.Delay(_startDelay.Value).ConfigureAwait(false);
				else
					await Task.Delay(_startDelay.Value, cancellationToken).ConfigureAwait(false);
			}

			if (_startException is not null)
				throw _startException;
		}
		finally
		{
			// Set in finally so it's recorded even on cancellation/fault — tests use this
			// to verify that StopAsync was not called until StartAsync fully unwound.
			StartCompletedAt = DateTime.UtcNow;
		}
	}

	public async Task StopAsync(CancellationToken cancellationToken = default)
	{
		// Record timestamp *before* any delay/exception so that tests can assert ordering
		// (StopCalledAt >= StartCompletedAt) even when StopAsync subsequently faults.
		StopCalledAt = DateTime.UtcNow;
		Interlocked.Increment(ref _stopCount);

		if (_stopDelay.HasValue)
			await Task.Delay(_stopDelay.Value, cancellationToken).ConfigureAwait(false);

		if (_stopException is not null)
			throw _stopException;
	}

	public IOpAmpRemoteConfigMessageSubscriber? CapturedSubscriber { get; private set; }

	public void SubscribeToRemoteConfigMessages(IOpAmpRemoteConfigMessageSubscriber subscriber) =>
		CapturedSubscriber = subscriber;

	public void Dispose() => Interlocked.Increment(ref _disposeCount);
}
