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
using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.Core;

internal static class ElasticOpenTelemetry
{
	private static volatile ElasticOpenTelemetryComponents? SharedComponents;

#pragma warning disable IDE0028 // Simplify collection initialization
	internal static readonly ConditionalWeakTable<object, BuilderState> BuilderStateTable = new();
#pragma warning restore IDE0028 // Simplify collection initialization

	private static readonly Lock Lock = new();

	internal static BootstrapInfo TryBootstrap(out ElasticOpenTelemetryComponents components) =>
		TryBootstrap(SdkActivationMethod.NuGet, CompositeElasticOpenTelemetryOptions.DefaultOptions, null, out components);

	internal static BootstrapInfo TryBootstrap(SdkActivationMethod activationMethod, out ElasticOpenTelemetryComponents components) =>
		TryBootstrap(activationMethod, CompositeElasticOpenTelemetryOptions.DefaultOptions, null, out components);

	internal static BootstrapInfo TryBootstrap(
		CompositeElasticOpenTelemetryOptions options,
		out ElasticOpenTelemetryComponents components) =>
			TryBootstrap(SdkActivationMethod.NuGet, options, null, out components);

	internal static BootstrapInfo TryBootstrap(
		IServiceCollection? services,
		out ElasticOpenTelemetryComponents components) =>
			TryBootstrap(SdkActivationMethod.NuGet, CompositeElasticOpenTelemetryOptions.DefaultOptions, services, out components);

	internal static BootstrapInfo TryBootstrap(
		CompositeElasticOpenTelemetryOptions options,
		IServiceCollection? services,
		out ElasticOpenTelemetryComponents components) =>
			TryBootstrap(SdkActivationMethod.NuGet, options, services, out components);

	/// <summary>
	/// Shared bootstrap routine for the Elastic Distribution of OpenTelemetry .NET.
	/// Used to ensure auto instrumentation and manual instrumentation bootstrap the same way.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static BootstrapInfo TryBootstrap(
		SdkActivationMethod activationMethod,
		CompositeElasticOpenTelemetryOptions options,
		IServiceCollection? services,
		out ElasticOpenTelemetryComponents components)
	{
		BootstrapInfo? bootstrapInfo = null;

		try
		{
			// If an IServiceCollection is provided, we attempt to access any existing
			// components to reuse them.
			if (TryGetExistingComponents(services, out var existingComponents))
			{
				components = existingComponents;
				return existingComponents.BootstrapInfo;
			}

			// We only expect this to be allocated a handful of times, generally once.
			var stackTrace = new StackTrace(true);

			// If an IServiceCollection is provided, but it doesn't yet include any components then
			// create new components.
			if (services is not null)
			{
				using (var scope = Lock.EnterScope())
				{
					if (TryGetExistingComponents(services, out existingComponents))
					{
						components = existingComponents;
						return existingComponents.BootstrapInfo;
					}

					bootstrapInfo = new BootstrapInfo(activationMethod, stackTrace);
					components = CreateComponents(bootstrapInfo, options, stackTrace);
				}

				services.AddSingleton(components);
				services.AddHostedService<ElasticOpenTelemetryService>();

				return bootstrapInfo;
			}

			// When no IServiceCollection is provided we attempt to avoid bootstrapping more than
			// once. The first call into Boostrap wins and thereafter the same components are reused.
			if (TryGetSharedComponents(SharedComponents, stackTrace, out var shared))
			{
				components = shared;
				return shared.BootstrapInfo;
			}

			using (var scope = Lock.EnterScope())
			{
				if (TryGetSharedComponents(SharedComponents, stackTrace, out shared))
				{
					components = shared;
					return shared.BootstrapInfo;
				}

				bootstrapInfo = new BootstrapInfo(activationMethod, stackTrace);
				components = SharedComponents = CreateComponents(bootstrapInfo, options, stackTrace);
				return bootstrapInfo;
			}
		}
		catch (Exception ex)
		{
			options.AdditionalLogger?.LogCritical(ex, "Unable to bootstrap the Elastic Distribution of OpenTelemetry .NET SDK.");
			bootstrapInfo = new(activationMethod, ex);
			components = ElasticOpenTelemetryComponents.CreateDefault(bootstrapInfo);

			return bootstrapInfo;
		}

		static bool TryGetExistingComponents(IServiceCollection? services, [NotNullWhen(true)] out ElasticOpenTelemetryComponents? components)
		{
			components = null;

			if (services?.FirstOrDefault(s => s.ServiceType == typeof(ElasticOpenTelemetryComponents))
				?.ImplementationInstance as ElasticOpenTelemetryComponents is not { } existingComponents)
				return false;

			existingComponents.Logger.LogComponentsReused(Environment.NewLine, new StackTrace(true));
			components = existingComponents;
			return true;
		}

		static bool TryGetSharedComponents(ElasticOpenTelemetryComponents? components, StackTrace stackTrace,
			[NotNullWhen(true)] out ElasticOpenTelemetryComponents? sharedComponents)
		{
			sharedComponents = null;

			if (components is null)
				return false;

			components.Logger.LogSharedComponentsReused(Environment.NewLine, stackTrace);
			sharedComponents = components;

			return true;
		}

		static ElasticOpenTelemetryComponents CreateComponents(
			BootstrapInfo bootstrap,
			CompositeElasticOpenTelemetryOptions options,
			StackTrace stackTrace)
		{
			var logger = new CompositeLogger(options);
			var eventListener = new LoggingEventListener(logger, options);
			var components = new ElasticOpenTelemetryComponents(bootstrap, logger, eventListener, options);

			logger.LogDistroPreamble(bootstrap.ActivationMethod, components);
			logger.LogElasticOpenTelemetryBootstrapped(Environment.NewLine, stackTrace);

			return components;
		}
	}
}
