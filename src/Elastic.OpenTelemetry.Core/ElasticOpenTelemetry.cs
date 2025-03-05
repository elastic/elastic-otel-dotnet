// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Elastic.OpenTelemetry.Core;

internal static class ElasticOpenTelemetry
{
	private static int BootstrapCounter;

	public static SdkActivationMethod ActivationMethod;

	internal static readonly HashSet<ElasticOpenTelemetryComponents> SharedComponents = [];

	private static readonly Lock Lock = new();

	internal static ElasticOpenTelemetryComponents Bootstrap(SdkActivationMethod activationMethod) =>
		Bootstrap(activationMethod, CompositeElasticOpenTelemetryOptions.DefaultOptions, null);

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

		// Strictly speaking, we probably don't require locking here as the registration of
		// OpenTelemetry is expected to run sequentially. That said, the overhead is low
		// since this is called infrequently.
		using (var scope = Lock.EnterScope())
		{
			// If an IServiceCollection is provided, we attempt to access any existing
			// components to reuse them before accessing any potential shared components.
			if (services is not null)
			{
				if (TryGetExistingComponentsFromServiceCollection(services, out var existingComponents))
				{
					existingComponents.Logger.LogBootstrapInvoked(invocationCount);
					return existingComponents;
				}
			}

			// We don't have components assigned for this IServiceCollection, attempt to use
			// the existing SharedComponents, or create components.
			if (TryGetSharedComponents(options, out var sharedComponents))
			{
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
			var logger = new CompositeLogger(options);
			var eventListener = new LoggingEventListener(logger, options);
			var components = new ElasticOpenTelemetryComponents(logger, eventListener, options);

			logger.LogDistroPreamble(activationMethod, components);
			logger.LogComponentsCreated(Environment.NewLine, stackTrace);

			return components;
		}
	}
}
