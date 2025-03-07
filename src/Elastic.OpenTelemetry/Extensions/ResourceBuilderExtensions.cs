// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics.CodeAnalysis;
using Elastic.OpenTelemetry.Core;
using Elastic.OpenTelemetry.Diagnostics;
using Elastic.OpenTelemetry.Instrumentation;
using Microsoft.Extensions.DependencyInjection;

#if NET
using System.Runtime.CompilerServices;
#endif

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace OpenTelemetry.Resources;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides extension methods on the <see cref="ResourceBuilder"/> used to register
/// the Elastic Distribution of OpenTelemetry (EDOT) .NET defaults.
/// </summary>
internal static class ResourceBuilderExtensions
{
	/// <summary>
	/// Used to track the number of times any variation of `WithElasticDefaults` is invoked by consuming
	/// code across all <see cref="ResourceBuilder"/> instances. This allows us to warn about potenital
	/// misconfigurations.
	/// </summary>
	private static int WithElasticDefaultsCallCount;

	internal static ResourceBuilder WithElasticDefaults(this ResourceBuilder builder, BuilderState builderState, IServiceCollection? services) =>
		WithElasticDefaults(builder, builderState.Components, services);

	internal static ResourceBuilder WithElasticDefaults(this ResourceBuilder builder, ElasticOpenTelemetryComponents components, IServiceCollection? services)
	{
		var callCount = Interlocked.Increment(ref WithElasticDefaultsCallCount);

		components.Logger.LogWithElasticDefaultsCallCount(callCount, nameof(ResourceBuilder));

		return SignalBuilder.WithElasticDefaults(builder, components.Options, components, services, (b, bs, _) => ConfigureBuilder(b, bs));
	}

	[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "The call to `AssemblyScanning.AddInstrumentationViaReflection` " +
		"is guarded by a RuntimeFeature.IsDynamicCodeSupported` check and therefore this method is safe to call in AoT scenarios.")]
	private static void ConfigureBuilder(ResourceBuilder builder, BuilderState builderState)
	{
		builder.AddHostDetector();
		builderState.Components.Logger.LogResourceDetectorAdded("HostDetector", builderState.InstanceIdentifier);

		builder.AddProcessRuntimeDetector();
		builderState.Components.Logger.LogResourceDetectorAdded("ProcessRuntimeDetector", builderState.InstanceIdentifier);

#if NET
		if (RuntimeFeature.IsDynamicCodeSupported)
#endif
		{
			SignalBuilder.AddInstrumentationViaReflection(builder, builderState.Components,
				ContribResourceDetectors.GetContribResourceDetectors(), builderState.InstanceIdentifier);
		}
	}
}
