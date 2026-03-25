// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections.Concurrent;
using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Core;
using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.Diagnostics;

/// <summary>
/// A composite logger for use inside the distribution which logs to the <see cref="FileLogger"/>
/// and optionally an additional <see cref="ILogger"/>.
/// </summary>
/// <remarks>
/// Supports a two-phase lifecycle: deferred mode (queues entries before config is finalized)
/// and active mode (creates sub-loggers, drains queue, normal operation).
/// When options are available and no OpAmp endpoint is configured, the logger activates immediately —
/// no deferral overhead is incurred. When an OpAmp endpoint is configured (central config may update the
/// options instance in place) or options are not yet available, the logger defers until
/// <see cref="Activate"/> is called. If the safety timer fires before activation and options
/// are available, the logger activates with the current state of those options (which may
/// already reflect central config mutations).
/// If disposed, triggers disposal of the <see cref="FileLogger"/>.
/// </remarks>
internal sealed class CompositeLogger : IDisposable, IAsyncDisposable, ILogger
{
	public const string LogCategory = "Elastic.OpenTelemetry";

	internal Guid InstanceId { get; } = Guid.NewGuid();

	private FileLogger? _fileLogger;
	private StandardOutLogger? _consoleLogger;
	private ILogger? _additionalLogger;
	// Not guarded by Interlocked — used only as a best-effort fast-exit check in Log(),
	// not as a dispose-once gate. Torn reads are harmless: the worst case is one extra
	// log entry slipping through during teardown.
	private bool _isDisposed;

	// Deferred mode infrastructure
	private ConcurrentQueue<DeferredLogEntry>? _deferredQueue;
	private System.Timers.Timer? _safetyTimer;
	private volatile CompositeElasticOpenTelemetryOptions? _options;

	// Per-instance lock guarding the deferred-to-active handoff.
	// Both Log() and Activate() acquire this to make the queue snapshot + enqueue
	// atomic with the queue capture + null transition, preventing late enqueues
	// into a stale queue that no consumer will ever drain.
	private readonly Lock _activationLock = new();

	// Tracks whether Activate() has been claimed by a thread. 0 = pending, 1 = claimed.
	// Interlocked.CompareExchange atomically swaps 0→1 and returns the old value, so only
	// the thread that sees 0 returned proceeds with activation — all others exit early.
	// This is separate from _activationLock: the CAS guarantees a single winner, while
	// the lock serialises the Log/Activate handoff so Log() never sees _deferredQueue=null
	// before sub-loggers are fully assigned.
	private int _activationState;

	// Static singleton for pre-activation sharing
	private static CompositeLogger? PreActivationInstance;
	private static readonly Lock StaticLock = new();

	/// <summary>
	/// Indicates whether file logging is enabled, based on the presence and state of the FileLogger.
	/// </summary>
	public bool LogFileEnabled => _fileLogger?.FileLoggingEnabled ?? false;

	/// <summary>
	/// Gets the file path of the log file used by the FileLogger, or an empty string if the FileLogger is not initialized.
	/// </summary>
	public string LogFilePath => _fileLogger?.LogFilePath ?? string.Empty;

	/// <summary>
	/// Creates a logger whose mode is determined by the provided options:
	/// <list type="bullet">
	///   <item>If options are provided and no OpAmp endpoint is configured, sub-loggers are created immediately (active mode).</item>
	///   <item>If an OpAmp endpoint is configured (central config may arrive), the logger defers (queues entries)
	///     until <see cref="Activate"/> is called with final config. The options reference is retained so the safety
	///     timer can activate with whatever state the options have at that point (including any central config mutations
	///     applied in place).</item>
	///   <item>If no options are provided, the logger defers. If the safety timer fires and no options have been
	///     provided via a subsequent <see cref="GetOrCreate"/> call, queued entries are discarded.</item>
	/// </list>
	/// </summary>
	public CompositeLogger(CompositeElasticOpenTelemetryOptions? options = null)
	{
		_options = options;

		if (options is not null && string.IsNullOrEmpty(options.OpAmpEndpoint))
		{
			// No OpAmp endpoint configured — no central config possible. Activate immediately.
			if (BootstrapLogger.IsEnabled)
			{
				BootstrapLogger.LogWithStackTrace($"{nameof(CompositeLogger)}: Instance '{InstanceId}' created in active mode." +
					$"{Environment.NewLine}    Invoked with `{nameof(CompositeElasticOpenTelemetryOptions)}` instance '{options.InstanceId}'.");
			}

			_fileLogger = new(options);
			_consoleLogger = new(options);
			_additionalLogger = options.AdditionalLogger;

			this.LogDebug("{ClassName} created in active mode.", nameof(CompositeLogger));

			// _deferredQueue stays null — active mode

			return;
		}

		// Either no options yet or OpAmp is enabled — defer until Activate is called
		if (BootstrapLogger.IsEnabled)
		{
			BootstrapLogger.Log($"{nameof(CompositeLogger)}: Instance '{InstanceId}' created in deferred mode." +
				(options is not null ? $" Options '{options.InstanceId}' retained for safety timer fallback." : " No options available."));
		}

		_deferredQueue = new ConcurrentQueue<DeferredLogEntry>();

		_safetyTimer = new System.Timers.Timer(ElasticOpenTelemetry.SafetyTimerMs);
		_safetyTimer.Elapsed += (_, _) => OnSafetyTimerElapsed();
		_safetyTimer.AutoReset = false;
		_safetyTimer.Start();

		this.LogDebug("{ClassName} created in deferred mode.", nameof(CompositeLogger));
	}

	/// <summary>
	/// Safety timer callback. If options are available, activates the logger.
	/// If no options are available, discards the deferred queue.
	/// Extracted as internal method for direct unit testing.
	/// </summary>
	internal void OnSafetyTimerElapsed()
	{
		// If we have options, activate with them. Since options are mutated in place by
		// SetLogLevelFromCentralConfig, they already reflect any central config updates.
		if (_options is not null)
		{
			Activate(_options);
			return;
		}

		// No options available at all — discard the queue
		if (BootstrapLogger.IsEnabled)
			BootstrapLogger.Log($"{nameof(CompositeLogger)}: Safety timer fired but no options available. " +
				$"Discarding {_deferredQueue?.Count ?? 0} deferred log entries for instance '{InstanceId}'.");

		using (_activationLock.EnterScope())
		{
			if (_deferredQueue is null)
				return;

			// Null out the queue so IsEnabled and Log treat this instance as inactive.
			// A subsequent Activate(options) call will still proceed correctly because
			// Activate's single-execution guard is now _activationState (via
			// Interlocked.CompareExchange), not _deferredQueue is null.
			_deferredQueue = null;
		}

		using (StaticLock.EnterScope())
		{
			if (ReferenceEquals(PreActivationInstance, this))
				PreActivationInstance = null;
		}

		if (_safetyTimer is not null)
		{
			_safetyTimer.Stop();
			_safetyTimer.Dispose();
			_safetyTimer = null;
		}
	}

	/// <summary>
	/// Returns the singleton instance, creating it if needed. The logger decides internally
	/// whether to defer based on the options:
	/// <list type="bullet">
	///   <item>If options are available and no OpAmp endpoint is configured, activates immediately (no deferral).</item>
	///   <item>If an OpAmp endpoint is configured (central config may update options in place), defers until <see cref="Activate"/> is called.</item>
	///   <item>If no options are available, defers until <see cref="Activate"/> is called.</item>
	/// </list>
	/// </summary>
	internal static CompositeLogger GetOrCreate(CompositeElasticOpenTelemetryOptions? options = null)
	{
		if (BootstrapLogger.IsEnabled)
			BootstrapLogger.LogWithStackTrace($"{nameof(CompositeLogger)}: {nameof(GetOrCreate)} invoked.");

		var existing = PreActivationInstance;
		if (existing is not null)
		{
			if (IsCompatible(existing, options))
			{
				// If options become available after initial creation, store them so the safety
				// timer can use them as fallback for activation.
				if (options is not null && existing._options is null)
					existing._options = options;

				if (BootstrapLogger.IsEnabled)
					BootstrapLogger.Log($"{nameof(CompositeLogger)}: {nameof(GetOrCreate)} returning existing instance.");

				return existing;
			}

			// Options don't match — this is a concurrent bootstrap flow (e.g., parallel tests).
			// Create an isolated instance that is NOT stored as the singleton.
			if (BootstrapLogger.IsEnabled)
				BootstrapLogger.Log($"{nameof(CompositeLogger)}: {nameof(GetOrCreate)} options mismatch, creating isolated instance.");

			return new CompositeLogger(options);
		}

		using (StaticLock.EnterScope())
		{
			if (PreActivationInstance is not null)
			{
				if (IsCompatible(PreActivationInstance, options))
				{
					if (options is not null && PreActivationInstance._options is null)
						PreActivationInstance._options = options;

					return PreActivationInstance;
				}

				return new CompositeLogger(options);
			}

			if (BootstrapLogger.IsEnabled)
				BootstrapLogger.Log($"{nameof(CompositeLogger)}: {nameof(GetOrCreate)} creating new instance.");

			PreActivationInstance = new CompositeLogger(options);
			return PreActivationInstance;
		}
	}

	/// <summary>
	/// Checks whether the existing singleton is compatible with the requested options.
	/// Compatible means: either side has no options (same bootstrap flow, options not yet available),
	/// or both have the same options reference or equal options.
	/// </summary>
	private static bool IsCompatible(CompositeLogger existing, CompositeElasticOpenTelemetryOptions? options)
	{
		// If either side has no options, they could be part of the same bootstrap flow
		if (options is null || existing._options is null)
			return true;

		// Same reference or equivalent options — same bootstrap flow
		return ReferenceEquals(existing._options, options) || existing._options.Equals(options);
	}

	/// <summary>
	/// Clears the static pre-activation singleton without activating it.
	/// Called when Bootstrap resolves to existing shared or service-collection components,
	/// meaning the pre-activation logger will not be adopted and should be discarded.
	/// </summary>
	internal static void ClearPreActivationInstance()
	{
		using (StaticLock.EnterScope())
		{
			PreActivationInstance = null;
		}
	}

	/// <summary>
	/// Transitions from deferred mode to active mode: creates sub-loggers, drains the queue,
	/// and clears the static singleton reference. If already active, clears the static reference
	/// and returns (idempotent).
	/// </summary>
	internal void Activate(CompositeElasticOpenTelemetryOptions options)
	{
		if (BootstrapLogger.IsEnabled)
			BootstrapLogger.Log($"{nameof(CompositeLogger)}: Activating instance '{InstanceId}' with options '{options.InstanceId}'.");

		// Claim the right to activate exactly once. Interlocked.CompareExchange atomically
		// swaps _activationState from 0 to 1 and returns the previous value. Only the thread
		// that sees 0 returned wins; every other concurrent or subsequent call exits here.
		// This prevents double-activation races that the previous _deferredQueue is null
		// guard could not fully prevent (it was checked outside _activationLock).
		if (Interlocked.CompareExchange(ref _activationState, 1, 0) != 0)
		{
			using (StaticLock.EnterScope())
			{
				if (ReferenceEquals(PreActivationInstance, this))
					PreActivationInstance = null;
			}

			return;
		}

		ConcurrentQueue<DeferredLogEntry>? capturedQueue;

		// Under per-instance lock: create sub-loggers, capture queue, null it out.
		// This is the same lock that Log() holds when checking + enqueuing, so no
		// thread can enqueue into the old queue after we've captured it.
		using (_activationLock.EnterScope())
		{
			// Create sub-loggers from final options while holding the lock so that
			// any Log() call that wins the lock after us sees active sub-loggers.
			_fileLogger = new FileLogger(options);
			_consoleLogger = new StandardOutLogger(options);
			_additionalLogger = options.AdditionalLogger;
			_options = null; // No longer needed — sub-loggers are created

			capturedQueue = _deferredQueue;
			_deferredQueue = null;
		}

		// Clear static singleton reference (separate concern, separate lock)
		using (StaticLock.EnterScope())
		{
			if (ReferenceEquals(PreActivationInstance, this))
				PreActivationInstance = null;
		}

		var count = 0;

		// Drain captured queue outside lock — no other thread can enqueue into it
		// since _deferredQueue is now null and the lock ensures visibility.
		if (capturedQueue is not null)
		{
			while (capturedQueue.TryDequeue(out var entry))
			{
				Log(entry.LogLevel, entry.EventId, entry.State, entry.Exception, entry.Formatter);
				count++;
			}
		}

		this.LogCompositeLoggerActivated(count);

		// Dispose safety timer
		if (_safetyTimer is not null)
		{
			_safetyTimer.Stop();
			_safetyTimer.Dispose();
			_safetyTimer = null;
		}
	}

	// Thread safety: Dispose/DisposeAsync are not individually guarded with Interlocked
	// because the only caller is ElasticOpenTelemetryComponents, whose own Interlocked.Exchange
	// guard guarantees exactly one winner. If CompositeLogger is ever disposed from multiple
	// sites concurrently, it would need its own thread-safe dispose guard.
	public void Dispose()
	{
		_isDisposed = true;
		_additionalLogger = null;
		_fileLogger?.Dispose();

		if (_safetyTimer is not null)
		{
			_safetyTimer.Stop();
			_safetyTimer.Dispose();
			_safetyTimer = null;
		}
	}

	public async ValueTask DisposeAsync()
	{
		_isDisposed = true;
		_additionalLogger = null;

		if (_fileLogger is not null)
			await _fileLogger.DisposeAsync().ConfigureAwait(false);

		if (_safetyTimer is not null)
		{
			_safetyTimer.Stop();
			_safetyTimer.Dispose();
			_safetyTimer = null;
		}
	}

	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		if (_isDisposed)
			return;

		// Fast path: if no deferred queue, skip straight to active logging.
		// This volatile read is sufficient for the common case (active mode).
		if (_deferredQueue is not null)
		{
			// Slow path: acquire per-instance lock to atomically check + enqueue.
			// This prevents the race where Activate() captures and drains the queue
			// between our snapshot and our enqueue.
			using (_activationLock.EnterScope())
			{
				var queue = _deferredQueue;
				if (queue is not null)
				{
					string BoxedFormatter(object? s, Exception? e) => formatter((TState)s!, e);
					queue.Enqueue(new DeferredLogEntry(logLevel, eventId, state, exception, BoxedFormatter));
					return;
				}
			}
			// Lock released and _deferredQueue is null — fall through to active logging
		}

		// Active mode — route to sub-loggers
		if (_fileLogger is not null && _fileLogger.IsEnabled(logLevel))
			_fileLogger.Log(logLevel, eventId, state, exception, formatter);

		if (_consoleLogger is not null && _consoleLogger.IsEnabled(logLevel))
			_consoleLogger.Log(logLevel, eventId, state, exception, formatter);

		if (_additionalLogger is not null && _additionalLogger.IsEnabled(logLevel))
			_additionalLogger.Log(logLevel, eventId, state, exception, formatter);
	}

	public void SetAdditionalLogger(ILogger logger, SdkActivationMethod activationMethod, ElasticOpenTelemetryComponents components)
	{
		if (HasAdditionalLogger)
		{
			components.Logger.LogWarning("An additional ILogger has already been set on the CompositeLogger. Ignoring subsequent attempt to set an additional ILogger.");
			return;
		}

		components.Logger.LogInformation("Added additional ILogger to composite logger.");

		_additionalLogger = logger;
		_additionalLogger.LogDistroPreamble(activationMethod, components);
	}

	internal bool HasAdditionalLogger => _additionalLogger is not null;

	public bool IsEnabled(LogLevel logLevel)
	{
		// In deferred mode, always return true so messages are queued
		if (_deferredQueue is not null)
			return true;

		return (_consoleLogger?.IsEnabled(logLevel) ?? false)
			|| (_fileLogger?.IsEnabled(logLevel) ?? false)
			|| (_additionalLogger?.IsEnabled(logLevel) ?? false);
	}

	public IDisposable BeginScope<TState>(TState state) where TState : notnull
	{
		// In deferred mode, return a no-op scope. This means scope context is lost for
		// log entries queued during deferral — an accepted trade-off given the short
		// deferral window (until Activate is called or the safety timer fires).
		if (_deferredQueue is not null)
			return NullScope.Instance;

		return new CompositeDisposable(_fileLogger?.BeginScope(state), _additionalLogger?.BeginScope(state));
	}

	private readonly record struct DeferredLogEntry(
		LogLevel LogLevel,
		EventId EventId,
		object? State,
		Exception? Exception,
		Func<object?, Exception?, string> Formatter
	);

	private class NullScope : IDisposable
	{
		public static readonly NullScope Instance = new();

		public void Dispose()
		{
			// No-op
		}
	}

	private class CompositeDisposable(params IDisposable?[] disposables) : IDisposable
	{
		public void Dispose()
		{
			foreach (var disposable in disposables)
				disposable?.Dispose();
		}
	}
}
