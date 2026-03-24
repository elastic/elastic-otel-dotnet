// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.OpAmp.Abstractions;
using Microsoft.Extensions.Logging;

#if NETFRAMEWORK || NET
using Elastic.OpenTelemetry.OpAmp;
#endif

#if NETFRAMEWORK
using System.Net.Http;
#endif

#if USE_ISOLATED_OPAMP_CLIENT
using System.Runtime.CompilerServices;
#endif

namespace Elastic.OpenTelemetry.Core.Configuration;

internal sealed class CentralConfiguration : IDisposable, IAsyncDisposable
{
	private static int OpAmpBlockingStartTimeoutMilliseconds => ElasticOpenTelemetry.OpAmpStartTimeoutMs;

#if NETFRAMEWORK || NET
	private static string UserAgent => $"elastic-opamp-dotnet/{VersionHelper.InformationalVersion}";
#endif

	private readonly ILogger _logger;
	private IOpAmpClient _client;
	private readonly RemoteConfigSubscriber _remoteConfigSubscriber;
	private int _disposed;

	internal CentralConfiguration(CompositeElasticOpenTelemetryOptions options, ILogger logger)
	{
		_logger = logger;
		_remoteConfigSubscriber = new RemoteConfigSubscriber(_logger);

		// ResolveOpAmpServiceIdentity must have been called before this point
		// (by CreateComponents) so that ServiceName is populated from ResourceAttributes.
		Debug.Assert(options.IdentityResolved,
			"CentralConfiguration created before ResolveOpAmpServiceIdentity was called.");

		if (options.IsOpAmpEnabled() is false)
		{
			_client = new EmptyOpAmpClient();
			_logger.LogOpAmpNotEnabled(nameof(CentralConfiguration));
			return;
		}

		_logger.LogInitializingCentralConfig(nameof(CentralConfiguration), options.OpAmpEndpoint);

		IOpAmpClient? client = null;

#if USE_ISOLATED_OPAMP_CLIENT
		if (RuntimeFeature.IsDynamicCodeSupported)
		{
			_logger.LogUsingIsolatedLoadContext(nameof(CentralConfiguration));

			try
			{
				// Use isolated load context to load OpAMP assemblies
				var loadContext = new OpAmpIsolatedLoadContext(_logger);

				client = loadContext.CreateOpAmpClientInstance(_logger,
					options.OpAmpEndpoint!,
					options.OpAmpHeaders ?? string.Empty,
					options.ServiceName!,
					options.ServiceVersion,
					UserAgent);
			}
			catch (Exception ex)
			{
				_logger.LogOpAmpClientCreationFailed(ex, nameof(CentralConfiguration), ex.GetType().Name);
			}
		}
		else
		{
			_logger.LogDynamicCodeNotSupported(nameof(CentralConfiguration));
		}
#elif NETFRAMEWORK || NET
		try
		{
			var factory = new ElasticOpAmpClientFactory();
			client = factory.Create(_logger,
				options.OpAmpEndpoint!,
				options.OpAmpHeaders ?? string.Empty,
				options.ServiceName!,
				options.ServiceVersion,
				UserAgent);
		}
		catch (Exception ex)
		{
			_logger.LogOpAmpClientCreationFailed(ex, nameof(CentralConfiguration), ex.GetType().Name);
		}
#else
		_logger.LogOpAmpNotSupportedOnPlatform(nameof(CentralConfiguration));
#endif

		if (client is not null)
		{
			_client = client;
			StartClient(_client);
			return;
		}

		_logger.LogUsingEmptyOpAmpClient(nameof(CentralConfiguration));
		_client = new EmptyOpAmpClient();
	}

	/// <summary>
	/// Test constructor that accepts a pre-built <see cref="IOpAmpClient"/> for fault injection testing.
	/// </summary>
	internal CentralConfiguration(IOpAmpClient client, ILogger logger)
	{
		_logger = logger;
		_remoteConfigSubscriber = new RemoteConfigSubscriber(_logger);
		_client = client;

		StartClient(_client);
	}

	// ── StartClient constants and types ──────────────────────────────────────────
	//
	// StartClient runs IOpAmpClient.StartAsync on a dedicated background thread so that
	// the constructor can enforce a bounded startup time even though StartAsync is async.
	//
	// Threading contract:
	//   - The WORKER thread owns the IOpAmpClient for the duration of StartAsync and any
	//     subsequent cleanup (StopAsync + Dispose + CTS disposal). It communicates outcome
	//     to the caller via an Interlocked-written int field.
	//   - The CALLER thread waits via Thread.Join(timeout). On success it retains the client;
	//     on any failure or timeout it swaps _client to EmptyOpAmpClient and never touches
	//     the original client again. This ownership split means cleanup never races with
	//     an in-flight StartAsync.
	//
	// Timeout budget (worst-case constructor block time):
	//   OpAmpBlockingStartTimeoutMilliseconds (2000)  — CTS fires, cancelling StartAsync
	//   + CleanupBudgetMs                     (500)   — worker runs StopAsync in finally
	//   + SchedulingMarginMs                  (500)   — OS thread scheduling headroom
	//   = 3000ms Join timeout

	/// <summary>Maximum time the worker's StopAsync cleanup is allowed to run.</summary>
	private const int CleanupBudgetMs = 500;

	/// <summary>Headroom for OS thread scheduling between cancellation and thread exit.</summary>
	private const int SchedulingMarginMs = 500;

	/// <summary>
	/// Outcome of the worker thread's StartAsync attempt. Stored as <c>int</c> because
	/// <see cref="Interlocked.Exchange(ref int, int)"/> requires an integral type.
	/// Worker writes via Interlocked.Exchange; caller reads via Volatile.Read after Join.
	/// </summary>
	private enum StartOutcome { Pending = 0, Succeeded = 1, Cancelled = 2, Faulted = 3 }

	private void StartClient(IOpAmpClient client)
	{
		client.SubscribeToRemoteConfigMessages(_remoteConfigSubscriber);
		_logger.LogOpAmpClientCreated(nameof(CentralConfiguration));

		var timeStamp = DateTime.UtcNow.Ticks;
		// No `using` — the worker thread owns CTS disposal in its finally block,
		// which may run after the caller has returned (if Join times out).
		var cts = new CancellationTokenSource(OpAmpBlockingStartTimeoutMilliseconds);
		var outcome = (int)StartOutcome.Pending;

		var thread = new Thread(() =>
		{
			// ── Worker lifecycle ──
			// 1. Try StartAsync with the CTS token
			// 2. Record outcome (Succeeded / Cancelled / Faulted)
			// 3. Finally: if not Succeeded → StopAsync + SafeDispose; always dispose CTS
			try
			{
				client.StartAsync(cts.Token).GetAwaiter().GetResult();
				Interlocked.Exchange(ref outcome, (int)StartOutcome.Succeeded);
			}
			catch (OperationCanceledException)
			{
				// Expected when CTS fires on timeout — not an error.
				Interlocked.Exchange(ref outcome, (int)StartOutcome.Cancelled);
			}
			catch (Exception ex)
			{
				_logger.LogOperationFaulted(nameof(CentralConfiguration),
					"StartAsync", ex.GetType().Name);
				Interlocked.Exchange(ref outcome, (int)StartOutcome.Faulted);
			}
			finally
			{
				// Three layers of protection here, from outside in:
				//
				// 1. Outer try/finally: guarantees cts.Dispose() runs unconditionally,
				//    even if StopAsync throws synchronously (see layer 2).
				//
				// 2. Inner try/catch around client.StopAsync(): IOpAmpClient is not our
				//    code. A non-async StopAsync implementation can throw synchronously
				//    before returning a Task — that exception would skip SafeDispose and
				//    escape the background thread (crashing the process) without this catch.
				//    TryWaitForCompletion already handles faulted *Tasks* internally; this
				//    catch covers the synchronous-throw edge case.
				//
				// 3. SafeDispose: wraps client.Dispose() in its own try/catch.
				try
				{
					// Same-thread read: the Interlocked.Exchange above and this cast are on
					// the same thread, so a plain cast is sufficient. Volatile.Read would add
					// no cross-thread guarantee here — reserve that for the caller's read.
					if ((StartOutcome)outcome != StartOutcome.Succeeded)
					{
						try
						{
							TryWaitForCompletion(client.StopAsync(), CleanupBudgetMs, _logger, "StopAsync (cleanup)");
						}
						catch (Exception ex)
						{
							_logger.LogOperationFaulted(nameof(CentralConfiguration),
								"StopAsync (cleanup)", ex.GetType().Name);
						}
						SafeDispose(client, _logger);
					}
				}
				finally
				{
					cts.Dispose();
				}
			}
		})
		{
			IsBackground = true,
			Name = "EDOT-OpAmp-Bootstrap"
		};

		thread.Start();

		// Join waits for actual thread termination — a stronger gate than signalling.
		// If Join returns true the worker is fully done (including its finally block),
		// so all cleanup has completed and the outcome field is in its final state.
		var joinTimeout = OpAmpBlockingStartTimeoutMilliseconds + CleanupBudgetMs + SchedulingMarginMs;
		var workerCompleted = thread.Join(joinTimeout);

		// When workerCompleted is true, Thread.Join provides a happens-before guarantee,
		// making the Volatile.Read technically redundant — but it makes the cross-thread
		// read intent explicit for readers of this code.
		// When workerCompleted is false, we skip the read entirely (use Pending).
		var result = workerCompleted
			? (StartOutcome)Volatile.Read(ref outcome)
			: StartOutcome.Pending;

		if (result == StartOutcome.Succeeded)
		{
			_logger.LogOpAmpClientStarted(nameof(CentralConfiguration),
				TimeSpan.FromTicks(DateTime.UtcNow.Ticks - timeStamp).TotalMilliseconds);
			return;
		}

		// All non-success paths end here. The caller never calls StopAsync/SafeDispose —
		// the worker owns cleanup in its finally block. The caller's only job is to swap
		// _client so that subsequent WaitForFirstConfig/Dispose calls short-circuit.
		//
		// If Join timed out (result == Pending), the worker is still alive. It will
		// eventually complete and clean up. The background thread terminates on process
		// exit. See "Known Tradeoff" in the spec for the edge case where the worker
		// succeeds after the caller has already swapped to empty.
		_logger.LogOpAmpClientStartTimeout(nameof(CentralConfiguration),
			OpAmpBlockingStartTimeoutMilliseconds);
		_client = new EmptyOpAmpClient();
	}

	internal bool WaitForFirstConfig(TimeSpan waitDuration)
	{
		if (_client is EmptyOpAmpClient)
			return false;

		return TryWaitForCompletion(_remoteConfigSubscriber.FirstConfigTask, waitDuration, _logger, "WaitForFirstConfig");
	}

	internal bool TryGetInitialConfig([NotNullWhen(true)] out ElasticRemoteConfig? config)
	{
		config = _remoteConfigSubscriber.InitialConfig;
		return config is not null;
	}

	// ── Normal-path lifecycle (Dispose / DisposeAsync) ──
	//
	// These only run when StartAsync *succeeded* and the client was retained.
	// If StartClient fell back to EmptyOpAmpClient, the guard short-circuits.
	// The worker thread's cleanup path is completely separate (see StartClient above).

	public void Dispose()
	{
		// EmptyOpAmpClient check: no real client to stop.
		// Interlocked.Exchange: exactly-once gate — first caller wins, all others bail out.
		if (_client is EmptyOpAmpClient || Interlocked.Exchange(ref _disposed, 1) != 0)
			return;

		_logger.LogDisposingOpAmpClient(nameof(CentralConfiguration));
		TryWaitForCompletion(_client.StopAsync(), 500, _logger, "StopAsync (Dispose)");
		SafeDispose(_client, _logger);
	}

	public async ValueTask DisposeAsync()
	{
		if (_client is EmptyOpAmpClient || Interlocked.Exchange(ref _disposed, 1) != 0)
			return;

		_logger.LogAsyncDisposingOpAmpClient(nameof(CentralConfiguration));

		try
		{
			await _client.StopAsync().WaitAsync(TimeSpan.FromMilliseconds(500)).ConfigureAwait(false);
		}
		catch (TimeoutException)
		{
			_logger.LogStopAsyncTimedOut(nameof(CentralConfiguration));
		}
		catch (Exception ex)
		{
			_logger.LogStopAsyncFaulted(nameof(CentralConfiguration), ex.GetType().Name);
		}

		SafeDispose(_client, _logger);
	}

	/// <summary>
	/// Synchronously waits for a <see cref="Task"/> with a timeout. Used on the worker thread
	/// and in <see cref="Dispose"/> where an async context is not available.
	/// <see cref="Task.Wait(int)"/> surfaces faulted Tasks as <see cref="AggregateException"/>
	/// (unlike <c>GetAwaiter().GetResult()</c> which unwraps to the inner exception).
	/// </summary>
	private static bool TryWaitForCompletion(Task task, int timeoutMs, ILogger logger, string operationName)
	{
		try
		{
			if (task.Wait(timeoutMs))
				return true;

			logger.LogOperationTimedOut(nameof(CentralConfiguration), operationName, timeoutMs);
			return false;
		}
		catch (AggregateException ex)
		{
			logger.LogOperationFaulted(nameof(CentralConfiguration), operationName, ex.InnerException?.GetType().Name ?? ex.GetType().Name);
			return false;
		}
	}

	private static bool TryWaitForCompletion(Task task, TimeSpan timeout, ILogger logger, string operationName) =>
		TryWaitForCompletion(task, (int)timeout.TotalMilliseconds, logger, operationName);

	/// <summary>
	/// Wraps <see cref="IDisposable.Dispose"/> with exception swallowing. <see cref="IOpAmpClient"/>
	/// is external code — a Dispose fault must not escape and mask an earlier exception or crash
	/// a background thread.
	/// </summary>
	private static void SafeDispose(IDisposable disposable, ILogger logger)
	{
		try
		{
			disposable.Dispose();
		}
		catch (Exception ex)
		{
			logger.LogDisposeFaulted(nameof(CentralConfiguration), ex.GetType().Name);
		}
	}

	private class RemoteConfigSubscriber(ILogger logger) : IOpAmpRemoteConfigMessageSubscriber
	{
		private readonly TaskCompletionSource<bool> _taskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private readonly ILogger _logger = logger;
		private ElasticRemoteConfig? _initialConfig;

		public void HandleMessage(ElasticRemoteConfig elasticRemoteConfig)
		{
			_logger.LogReceivedRemoteConfigMessage(nameof(RemoteConfigSubscriber), nameof(HandleMessage));

			if (Interlocked.CompareExchange(ref _initialConfig, elasticRemoteConfig, null) == null)
			{
				_logger.LogReceivedInitialCentralConfig(nameof(RemoteConfigSubscriber), nameof(HandleMessage));
				_taskCompletionSource.TrySetResult(true);
			}
		}

		// Paired with Interlocked.CompareExchange in HandleMessage
		internal ElasticRemoteConfig? InitialConfig => Volatile.Read(ref _initialConfig);

		internal Task FirstConfigTask => _taskCompletionSource.Task;
	}
}
