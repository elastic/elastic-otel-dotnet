// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Core.Configuration;
using Elastic.OpenTelemetry.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.Core;

internal static class ElasticOpenTelemetry
{
	/// <summary>Maximum time to wait for the OpAmp client to start.</summary>
	internal const int OpAmpStartTimeoutMs = 2000;

	/// <summary>Maximum time to wait for the first central config response after OpAmp starts.</summary>
	internal const int WaitForFirstConfigTimeoutMs = 3000;

	/// <summary>Extra margin to ensure the safety timer fires after the bootstrap path completes.</summary>
	internal const int SafetyTimerMarginMs = 1000;

	/// <summary>
	/// Safety timer for CompositeLogger deferred mode.
	/// Computed as OpAmpStartTimeout + WaitForFirstConfigTimeout + margin so the timer
	/// fires only after the normal bootstrap path has had time to activate the logger.
	/// </summary>
	internal const int SafetyTimerMs = OpAmpStartTimeoutMs + WaitForFirstConfigTimeoutMs + SafetyTimerMarginMs;

	private static int BootstrapCounter;

	public static SdkActivationMethod ActivationMethod;

	internal static readonly HashSet<ElasticOpenTelemetryComponents> SharedComponents = [];

	private static readonly Lock Lock = new();

	internal static ElasticOpenTelemetryComponents Bootstrap(SdkActivationMethod activationMethod) =>
		Bootstrap(activationMethod, CompositeElasticOpenTelemetryOptions.DefaultOptions, null);

	/// <summary>
	/// This checks for any existing components on the IServiceCollection and reuse them if found.
	/// It also attempts to used a shared components instance if available and suitable.
	/// If neither are available, it will create a new instance.
	/// </summary>
	internal static ElasticOpenTelemetryComponents Bootstrap(CompositeElasticOpenTelemetryOptions options, IServiceCollection? services) =>
		Bootstrap(SdkActivationMethod.NuGet, options, services);

	/// <summary>
	/// Shared bootstrap routine for the Elastic Distribution of OpenTelemetry .NET.
	/// Used to ensure auto instrumentation and manual instrumentation bootstrap the same way.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static ElasticOpenTelemetryComponents Bootstrap(
		SdkActivationMethod activationMethod,
		CompositeElasticOpenTelemetryOptions options,
		IServiceCollection? services)
	{
		var invocationCount = Interlocked.Increment(ref BootstrapCounter);

		if (BootstrapLogger.IsEnabled)
		{
			BootstrapLogger.LogWithStackTrace($"{nameof(Bootstrap)}: Static ctor invoked with count {invocationCount}." +
				$"{Environment.NewLine}    Invoked with `{nameof(CompositeElasticOpenTelemetryOptions)}` instance '{options.InstanceId}'.");

			BootstrapLogger.Log($"{nameof(Bootstrap)}: Services is {(services is null ? "`null`" : "not `null`")}");
		}

		ActivationMethod = activationMethod;

		ElasticOpenTelemetryComponents components;

		// We only expect this to be allocated a handful of times, generally once.
		var stackTrace = new StackTrace(true);

		// Strictly speaking, we probably don't require locking here as the registration of
		// OpenTelemetry is expected to run sequentially. That said, the overhead is low
		// since this is called infrequently.
		using (Lock.EnterScope())
		{
			// If an IServiceCollection is provided, we attempt to access any existing
			// components to reuse them before accessing any potential shared components.
			if (services is not null)
			{
				if (TryGetExistingComponentsFromServiceCollection(services, out var existingComponents))
				{
					if (BootstrapLogger.IsEnabled)
					{
						BootstrapLogger.Log($"{nameof(Bootstrap)}: Existing components instance '{existingComponents.InstanceId}' loaded from the `IServiceCollection`." +
							$"{Environment.NewLine}   With `{nameof(CompositeLogger)}` instance '{existingComponents.Logger.InstanceId}'.");
					}

					// Clear any pre-activation singleton that won't be adopted
					CompositeLogger.ClearPreActivationInstance();

					existingComponents.Logger.LogBootstrapInvoked(invocationCount);
					return existingComponents;
				}

				BootstrapLogger.Log($"{nameof(Bootstrap)}: No existing components available from the `IServiceCollection`.");
			}

			// We don't have components assigned for this IServiceCollection, attempt to use
			// the existing SharedComponents, or create components.
			if (TryGetSharedComponents(options, out var sharedComponents))
			{
				if (BootstrapLogger.IsEnabled)
				{
					BootstrapLogger.Log($"{nameof(Bootstrap)}: Existing components loaded from the shared components.");
					BootstrapLogger.Log($"{nameof(Bootstrap)}: Existing ElasticOpenTelemetryComponents instance '{sharedComponents.InstanceId}'.");
					BootstrapLogger.Log($"{nameof(Bootstrap)}: Existing CompositeLogger instance '{sharedComponents.Logger.InstanceId}'.");
				}

				// Clear any pre-activation singleton that won't be adopted
				CompositeLogger.ClearPreActivationInstance();

				components = sharedComponents;
			}
			else
			{
				components = CreateComponents(activationMethod, options, stackTrace);
				components.Logger.LogSharedComponentsNotReused();
				SharedComponents.Add(components);
			}

			components.Logger.LogBootstrapInvoked(invocationCount);

			services?.AddSingleton(components);

			return components;
		}

		static bool TryGetExistingComponentsFromServiceCollection(IServiceCollection? services, [NotNullWhen(true)] out ElasticOpenTelemetryComponents? components)
		{
			components = null;

			if (services?.FirstOrDefault(s => s.ServiceType == typeof(ElasticOpenTelemetryComponents))
				?.ImplementationInstance as ElasticOpenTelemetryComponents is not { } existingComponents)
				return false;

			existingComponents.Logger.LogServiceCollectionComponentsReused();
			components = existingComponents;
			return true;
		}

		static bool TryGetSharedComponents(CompositeElasticOpenTelemetryOptions options,
			[NotNullWhen(true)] out ElasticOpenTelemetryComponents? sharedComponents)
		{
			sharedComponents = null;

			foreach (var cachedComponents in SharedComponents)
			{
				if (cachedComponents.Options.Equals(options))
				{
					sharedComponents = cachedComponents;
					cachedComponents.Logger.LogSharedComponentsReused();
					return true;
				}
			}

			return false;
		}

		static ElasticOpenTelemetryComponents CreateComponents(
			SdkActivationMethod activationMethod,
			CompositeElasticOpenTelemetryOptions options,
			StackTrace stackTrace)
		{
			if (BootstrapLogger.IsEnabled)
				BootstrapLogger.Log($"{nameof(ElasticOpenTelemetry)}: CreateComponents invoked.");

			var logger = CompositeLogger.GetOrCreate(options);
			CentralConfiguration? centralConfig = null;

			// Resolve service identity from resource attributes before checking enablement.
			// IsOpAmpEnabled() is a pure query — it does not extract ServiceName/ServiceVersion.
			options.ResolveOpAmpServiceIdentity(logger);

			if (options.IsOpAmpEnabled(logger))
			{
				logger.LogInformation("{ClassName}.{MethodName}: OpAMP is enabled, attempting to fetch central configuration.", nameof(ElasticOpenTelemetry), nameof(CreateComponents));

				centralConfig = new CentralConfiguration(options, logger);

				if (centralConfig.WaitForFirstConfig(TimeSpan.FromMilliseconds(WaitForFirstConfigTimeoutMs)) && centralConfig.TryGetInitialConfig(out var config))
				{
					logger.LogDebug("{ClassName}.{MethodName}: Successfully retrieved initial central configuration.", nameof(ElasticOpenTelemetry), nameof(CreateComponents));

					if (config.LogLevel is not null)
					{
						options.SetLogLevelFromCentralConfig(config.LogLevel, logger);
					}
				}
				else
				{
					logger.LogWarning("{ClassName}.{MethodName}: Failed to retrieve central configuration within the timeout period, proceeding with local configuration.",
						nameof(ElasticOpenTelemetry), nameof(CreateComponents));
				}
			}
			else
			{
				logger.LogInformation("{ClassName}.{MethodName}: OpAMP is not enabled, skipping central configuration fetch.",
					nameof(ElasticOpenTelemetry), nameof(CreateComponents));
			}

			// Activate with final config — creates sub-loggers, drains deferred queue
			logger.Activate(options);

			var eventListener = new LoggingEventListener(logger, options);
			var components = new ElasticOpenTelemetryComponents(logger, eventListener, options, centralConfig);

			if (BootstrapLogger.IsEnabled)
			{
				BootstrapLogger.Log($"{nameof(CreateComponents)}: Created new CompositeLogger instance '{logger.InstanceId}' via CreateComponents.");
				BootstrapLogger.Log($"{nameof(CreateComponents)}: Created new LoggingEventListener instance '{eventListener.InstanceId}' via CreateComponents.");
				BootstrapLogger.Log($"{nameof(CreateComponents)}: Created new ElasticOpenTelemetryComponents instance '{components.InstanceId}' via CreateComponents.");
			}

			logger.LogDistroPreamble(activationMethod, components);
			logger.LogComponentsCreated(Environment.NewLine, stackTrace);

			return components;
		}
	}
}
