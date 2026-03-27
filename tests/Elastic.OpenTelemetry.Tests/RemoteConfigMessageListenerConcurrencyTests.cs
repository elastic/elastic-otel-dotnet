// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Reflection;
using Elastic.OpenTelemetry.OpAmp;
using Elastic.OpenTelemetry.OpAmp.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Elastic.OpenTelemetry.Tests;

/// <summary>
/// Stress tests for <see cref="RemoteConfigMessageListener.Subscribe"/> verifying the
/// lock-free CAS-based subscription pattern is safe under contention.
/// </summary>
public class RemoteConfigMessageListenerConcurrencyTests
{
	private static readonly FieldInfo SubscribersField =
		typeof(RemoteConfigMessageListener).GetField("_subscribers", BindingFlags.NonPublic | BindingFlags.Instance)!;

	[Fact]
	public void Subscribe_ConcurrentCalls_AllSubscribersRegistered()
	{
		// Validates that the CAS retry loop in Subscribe correctly handles contention:
		// all 16 subscribers must be present in the internal array with no duplicates
		// or losses. Verifies registration only (not message dispatch) because
		// RemoteConfigMessage is an external protobuf type that is difficult to
		// construct in isolation.
		const int threadCount = 16;
		var listener = new RemoteConfigMessageListener(NullLogger.Instance);
		var subscribers = new CountingSubscriber[threadCount];
		for (var i = 0; i < threadCount; i++)
			subscribers[i] = new CountingSubscriber();

		using var barrier = new Barrier(threadCount);
		var threads = new Thread[threadCount];

		for (var i = 0; i < threadCount; i++)
		{
			var sub = subscribers[i];
			threads[i] = new Thread(() =>
			{
				barrier.SignalAndWait();
				listener.Subscribe(sub);
			});
			threads[i].Start();
		}

		foreach (var t in threads)
			t.Join();

		var registered = (IOpAmpRemoteConfigMessageSubscriber[])SubscribersField.GetValue(listener)!;
		Assert.Equal(threadCount, registered.Length);

		// Verify every unique subscriber instance is present — no duplicates, no losses.
		var set = new HashSet<IOpAmpRemoteConfigMessageSubscriber>(registered);
		Assert.Equal(threadCount, set.Count);
		foreach (var sub in subscribers)
			Assert.Contains(sub, set);
	}

	[Fact]
	public void SnapshotIteration_DuringConcurrentSubscribe_DoesNotThrow()
	{
		// Validates that snapshot-based iteration is safe while Subscribe concurrently
		// swaps the _subscribers array reference via CAS. This mirrors the pattern used
		// by HandleMessage (Volatile.Read → foreach over the snapshot) but reads the
		// internal field directly rather than calling HandleMessage, which would require
		// constructing the external RemoteConfigMessage protobuf type. The safety
		// guarantee is identical: once a snapshot reference is captured, the array is
		// immutable — concurrent Subscribe calls create new arrays without affecting
		// any in-flight iteration.
		const int subscribeIterations = 1_000;
		const int readIterations = 2_000;
		var listener = new RemoteConfigMessageListener(NullLogger.Instance);

		// Seed one subscriber so the array is non-empty from the start.
		listener.Subscribe(new CountingSubscriber());

		Exception? caughtException = null;

		var subscriberThread = new Thread(() =>
		{
			for (var i = 0; i < subscribeIterations; i++)
				listener.Subscribe(new CountingSubscriber());
		});

		var readerThread = new Thread(() =>
		{
			try
			{
				for (var i = 0; i < readIterations; i++)
				{
					// Mirror the HandleMessage pattern: take a snapshot and iterate.
					var snapshot = (IOpAmpRemoteConfigMessageSubscriber[])SubscribersField.GetValue(listener)!;
					foreach (var sub in snapshot)
						_ = sub.GetHashCode(); // force iteration over each element
				}
			}
			catch (Exception ex)
			{
				Volatile.Write(ref caughtException, ex);
			}
		});

		subscriberThread.Start();
		readerThread.Start();

		subscriberThread.Join();
		readerThread.Join();

		Assert.Null(Volatile.Read(ref caughtException));

		// Sanity: verify the final subscriber count is correct.
		var registered = (IOpAmpRemoteConfigMessageSubscriber[])SubscribersField.GetValue(listener)!;
		Assert.Equal(1 + subscribeIterations, registered.Length);
	}

	private sealed class CountingSubscriber : IOpAmpRemoteConfigMessageSubscriber
	{
		private int _callCount;
		public int CallCount => Volatile.Read(ref _callCount);

		public void HandleMessage(ElasticRemoteConfig elasticRemoteConfig) =>
			Interlocked.Increment(ref _callCount);
	}
}
