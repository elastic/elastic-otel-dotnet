// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.OpAmp.Abstractions;

namespace Elastic.OpenTelemetry.Tests.Configuration;

/// <summary>
/// Test double whose <see cref="StopAsync"/> is non-async and throws synchronously
/// before returning a <see cref="Task"/>. This exercises the try/catch guard in
/// <c>CentralConfiguration.StartClient</c> that prevents a synchronous throw from
/// escaping the worker's finally block and crashing the background thread.
/// </summary>
internal sealed class SyncThrowingOpAmpClient : IOpAmpClient
{
	private readonly TimeSpan? _startDelay;
	private int _disposeCount;

	public bool StartCalled { get; private set; }
	public bool Disposed => _disposeCount > 0;

	public SyncThrowingOpAmpClient(TimeSpan? startDelay = null) => _startDelay = startDelay;

	public async Task StartAsync(CancellationToken cancellationToken = default)
	{
		StartCalled = true;

		if (_startDelay.HasValue)
			await Task.Delay(_startDelay.Value, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Non-async — throws synchronously before any Task is returned.
	/// Simulates a buggy upstream that fails in a pre-condition check.
	/// </summary>
	public Task StopAsync(CancellationToken cancellationToken = default) =>
		throw new InvalidOperationException("sync throw from StopAsync");

	public void SubscribeToRemoteConfigMessages(IOpAmpRemoteConfigMessageSubscriber subscriber) { }

	public void Dispose() => Interlocked.Increment(ref _disposeCount);
}
