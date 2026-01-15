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
			BootstrapLogger.Log($"{nameof(Bootstrap)}: CreateComponents invoked.");
			
			var logger = new CompositeLogger(options);

			if (options.IsOpAmpEnabled())
			{
				var centralConfig = new CentralConfiguration(options, logger);
				var remoteConfig = centralConfig.WaitForRemoteConfig(30000);

				if (remoteConfig is not null && remoteConfig.AgentConfigMap.ContainsKey("elastic"))
				{
					// TODO - Log

					var config = remoteConfig.AgentConfigMap["elastic"];

					if (config.ContentType == "application/json")
					{
						var body = config.Body;

						var utf8JsonSpan = "\"log_level\":\""u8;

						var index = body.IndexOf(utf8JsonSpan);

						// NOTE, this DOES NOT handle pretty-print JSON with new lines and spaces

						if (index >= 0)
						{
							var logLevelStart = index + utf8JsonSpan.Length;

							var logLevelEnd = body[logLevelStart..].IndexOf((byte)'"');
							if (logLevelEnd == -1)
								logLevelEnd = body.Length;

							var logLevelBytes = body[logLevelStart..][..logLevelEnd];
							var logLevelString = System.Text.Encoding.UTF8.GetString(logLevelBytes);
							if (Enum.TryParse<LogLevel>(logLevelString, true, out var logLevel))
							{
								options.SetLogLevelFromCentralConfig(logLevel);
							}
						}
					}
				}
			}

			if (DeferredLogger.TryGetInstance(out var deferredLogger))
			{
				deferredLogger.DrainAndRelease(logger);
			}

			var eventListener = new LoggingEventListener(logger, options);
			var components = new ElasticOpenTelemetryComponents(logger, eventListener, options);

			if (BootstrapLogger.IsEnabled)
			{
				BootstrapLogger.Log($"{nameof(Bootstrap)}: Created new CompositeLogger instance '{logger.InstanceId}' via CreateComponents.");
				BootstrapLogger.Log($"{nameof(Bootstrap)}: Created new LoggingEventListener instance '{eventListener.InstanceId}' via CreateComponents.");
				BootstrapLogger.Log($"{nameof(Bootstrap)}: Created new ElasticOpenTelemetryComponents instance '{components.InstanceId}' via CreateComponents.");
			}

			logger.LogDistroPreamble(activationMethod, components);
			logger.LogComponentsCreated(Environment.NewLine, stackTrace);

			return components;
		}
	}
}
