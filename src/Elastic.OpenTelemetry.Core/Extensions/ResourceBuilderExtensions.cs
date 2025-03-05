// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics.CodeAnalysis;
using Elastic.OpenTelemetry.Core;
using Elastic.OpenTelemetry.Diagnostics;
using Elastic.OpenTelemetry.Instrumentation;
using Elastic.OpenTelemetry.SemanticConventions;
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
	/// code acrosss all <see cref="ResourceBuilder"/> instances. This allows us to warn about potenital
	/// misconfigurations.
	/// </summary>
	private static int WithElasticDefaultsCallCount;

	/// <summary>
	/// The unique ID for this instance of the application/service.
	/// </summary>
	private static readonly string ApplicationInstanceId = Guid.NewGuid().ToString();

	internal static ResourceBuilder WithElasticDefaults(this ResourceBuilder builder, BuilderState builderState, IServiceCollection? services) =>
		WithElasticDefaults(builder, builderState.Components, services);

	internal static ResourceBuilder WithElasticDefaults(this ResourceBuilder builder, ElasticOpenTelemetryComponents components, IServiceCollection? services)
	{
		var callCount = Interlocked.Increment(ref WithElasticDefaultsCallCount);

		components.Logger.LogWithElasticDefaultsCallCount(callCount, nameof(ResourceBuilder));

		return SignalBuilder.WithElasticDefaults(builder, components.Options, components, services, ConfigureBuilder);
	}

	[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "The call to `AssemblyScanning.AddInstrumentationViaReflection` " +
		"is guarded by a RuntimeFeature.IsDynamicCodeSupported` check and therefore this method is safe to call in AoT scenarios.")]
	private static void ConfigureBuilder(ResourceBuilder builder, BuilderState builderState, IServiceCollection? services)
	{
		var attributes = new Dictionary<string, object>
		{
			{ ResourceSemanticConventions.AttributeServiceInstanceId, ApplicationInstanceId },
			{ ResourceSemanticConventions.AttributeTelemetryDistroName, "elastic" },
			{ ResourceSemanticConventions.AttributeTelemetryDistroVersion, VersionHelper.InformationalVersion }
		};

		builder.AddAttributes(attributes);

		foreach (var attribute in attributes)
		{
			if (attribute.Value is not null)
			{
				var value = attribute.Value.ToString() ?? "<empty>";
				builderState.Components.Logger.LogAddingResourceAttribute(attribute.Key, value, builderState.InstanceIdentifier);
			}
		}

#if NET
		if (RuntimeFeature.IsDynamicCodeSupported)
#endif
		{
			// Currently, this adds just the HostDetector if the DLL is present. This ensures that this method can be called
			// by auto-instrumentation which won't ship with that DLL. For the NuGet package, we depend on it so it will always
			// be present.
			SignalBuilder.AddInstrumentationViaReflection(builder, builderState.Components,
				ContribResourceDetectors.GetContribResourceDetectors(), builderState.InstanceIdentifier);
		}
	}
}
