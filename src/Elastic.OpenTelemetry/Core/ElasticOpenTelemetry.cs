// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Diagnostics;
using Elastic.OpenTelemetry.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Elastic.OpenTelemetry.Core;

internal static class ElasticOpenTelemetry
{
	private static volatile ElasticOpenTelemetryComponents? SharedComponents;
	private static int BootstrapCounter;

	public static SdkActivationMethod ActivationMethod;

#pragma warning disable IDE0028 // Simplify collection initialization
	internal static readonly ConditionalWeakTable<object, BuilderState> BuilderStateTable = new();
#pragma warning restore IDE0028 // Simplify collection initialization

	private static readonly Lock Lock = new();

	internal static ElasticOpenTelemetryComponents Bootstrap() =>
		Bootstrap(SdkActivationMethod.NuGet, CompositeElasticOpenTelemetryOptions.DefaultOptions, null);

	internal static ElasticOpenTelemetryComponents Bootstrap(SdkActivationMethod activationMethod) =>
		Bootstrap(activationMethod, CompositeElasticOpenTelemetryOptions.DefaultOptions, null);

	internal static ElasticOpenTelemetryComponents Bootstrap(CompositeElasticOpenTelemetryOptions options) =>
		Bootstrap(SdkActivationMethod.NuGet, options, null);

	internal static ElasticOpenTelemetryComponents Bootstrap(IServiceCollection? services) =>
		Bootstrap(SdkActivationMethod.NuGet, CompositeElasticOpenTelemetryOptions.DefaultOptions, services);

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
		ActivationMethod = activationMethod;

		ElasticOpenTelemetryComponents components;

		// We only expect this to be allocated a handful of times, generally once.
		var stackTrace = new StackTrace(true);

		var invocationCount = Interlocked.Increment(ref BootstrapCounter);

		// If an IServiceCollection is provided, we attempt to access any existing
		// components to reuse them.
		if (TryGetExistingComponentsFromServiceCollection(services, out var existingComponents))
		{
			existingComponents.Logger.LogBootstrapInvoked(invocationCount);
			return existingComponents;
		}

		// If an IServiceCollection is provided, but it doesn't yet include any components then
		// create new components.
		if (services is not null)
		{
			// Strictly speaking, we probably don't require locking here as the registration of
			// OpenTelemetry is expected to run sequentially. That said, the overhead is low
			// since this is called infrequently.
			using (var scope = Lock.EnterScope())
			{
				// Double-check that no components were created in a race situation.
				if (TryGetExistingComponentsFromServiceCollection(services, out existingComponents))
				{
					existingComponents.Logger.LogBootstrapInvoked(invocationCount);
					return existingComponents;
				}

				// We don't have components assigned for this IServiceCollection, attempt to use
				// the existing SharedComponents, or create components.
				if (TryGetSharedComponents(SharedComponents, stackTrace, out var sharedComponents)
					&& sharedComponents.Options.Equals(options))
				{
					components = sharedComponents;
				}
				else
				{
					components = SharedComponents = CreateComponents(activationMethod, options, stackTrace);
				}

				components.Logger.LogBootstrapInvoked(invocationCount);
			}

			services.AddSingleton(components);
			services.AddHostedService<ElasticOpenTelemetryService>();

			return components;
		}

		// When no IServiceCollection is provided we attempt to avoid bootstrapping more than
		// once. The first call into Bootstrap wins and thereafter the same components are reused.
		if (TryGetSharedComponents(SharedComponents, stackTrace, out var shared))
		{
			// We compare whether the options (values) equal those from the shared components. If the
			// values of the options differ, we will not reuse the shared components.
			if (shared.Options.Equals(options))
			{
				components = shared;
				components.Logger.LogSharedComponentsReused();
				return components;
			}

			components = CreateComponents(activationMethod, options, stackTrace);
			components.Logger.LogSharedComponentsNotReused();
			return components;
		}

		using (var scope = Lock.EnterScope())
		{
			if (TryGetSharedComponents(SharedComponents, stackTrace, out shared) && shared.Options.Equals(options))
			{
				components = shared;
				return components;
			}

			// If we get this far, we've been unable to get the shared components
			components = SharedComponents = CreateComponents(activationMethod, options, stackTrace);
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

		static bool TryGetSharedComponents(ElasticOpenTelemetryComponents? components, StackTrace stackTrace,
			[NotNullWhen(true)] out ElasticOpenTelemetryComponents? sharedComponents)
		{
			sharedComponents = null;

			if (components is null)
				return false;

			sharedComponents = components;

			return true;
		}

		static ElasticOpenTelemetryComponents CreateComponents(
			SdkActivationMethod activationMethod,
			CompositeElasticOpenTelemetryOptions options,
			StackTrace stackTrace)
		{
			var logger = new CompositeLogger(options);
			var eventListener = new LoggingEventListener(logger, options);
			var components = new ElasticOpenTelemetryComponents(logger, eventListener, options);

			logger.LogDistroPreamble(activationMethod, components);
			logger.LogComponentsCreated(Environment.NewLine, stackTrace);

			return components;
		}
	}

	/// <summary>
	/// Used for testing.
	/// </summary>
	internal static void ResetSharedComponentsForTesting() => SharedComponents = null;
}
