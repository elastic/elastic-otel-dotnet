// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Core;
using Elastic.OpenTelemetry.Diagnostics;
using Elastic.OpenTelemetry.SemanticConventions;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Elastic.OpenTelemetry.Resources;
#pragma warning restore IDE0130 // Namespace does not match folder structure

internal static class ResourceBuilderExtensions
{
	/// <summary>
	/// The unique ID for this instance of the application/service.
	/// </summary>
	private static readonly string ApplicationInstanceId = Guid.NewGuid().ToString();

	/// <summary>
	/// Used to track the number of times any variation of `WithElasticDefaults` is invoked by consuming
	/// code acrosss all <see cref="ResourceBuilder"/> instances. This allows us to warn about potenital
	/// misconfigurations.
	/// </summary>
	private static int WithElasticDefaultsCallCount;

	internal static ResourceBuilder WithElasticDefaultsCore(
		this ResourceBuilder builder,
		BuilderState builderState,
		IServiceCollection? services,
		Action<ResourceBuilder, BuilderState>? configure) =>
			WithElasticDefaultsCore(builder, builderState.Components, services, configure);

	internal static ResourceBuilder WithElasticDefaultsCore
		(this ResourceBuilder builder,
		ElasticOpenTelemetryComponents components,
		IServiceCollection? services,
		Action<ResourceBuilder, BuilderState>? configure)
	{
		var callCount = Interlocked.Increment(ref WithElasticDefaultsCallCount);

		components.Logger.LogWithElasticDefaultsCallCount(callCount, nameof(ResourceBuilder));

		return SignalBuilder.WithElasticDefaults(builder, components.Options, components, services,
			(ResourceBuilder builder, BuilderState builderState, IServiceCollection? services) =>
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

				configure?.Invoke(builder, builderState);
			});
	}
}
