// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Elastic.OpenTelemetry.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.Core;

internal static class SignalBuilder
{
	/// <summary>
	/// Hold common logic for configuring a builder, either a TracerProviderBuilder,
	/// MeterProviderBuilder or LoggingProviderBuilder.
	/// </summary>
	public static bool ConfigureBuilder<T>(
		string methodName,
		string builderName,
		T builder,
		GlobalProviderBuilderState globalProviderBuilderState,
		CompositeElasticOpenTelemetryOptions? options,
		IServiceCollection? services,
		Action<T, ElasticOpenTelemetryComponents> configure,
		[NotNullWhen(true)] ref ElasticOpenTelemetryComponents? components) where T : class
	{
		var callCount = globalProviderBuilderState.IncrementUseElasticDefaults();

		// If we are provided with options and components, we can avoid attempting to bootstrap again.
		// This scenario occurs if for example `AddElasticOpenTelemetry` is called multipled times
		// on the same `IServiceCollection`. In this case, a new `OpenTelemetryBuilder` would be
		// created (inside the SDK) for each call to `AddOpenTelemetry`, so the `BuilderState`, is
		// not be useful. `TryBootstrap` would be eventually reuse cached components registered
		// against the `IServiceCollection`, but we can still be more efficient to avoid calling that
		// code in this particular case by shortcutting and returning early.
		if (options is not null && components is not null)
		{
			ValidateGlobalCallCount(methodName, builderName, options, components, callCount);
			configure(builder, components);
			return true;
		}

		// This will later be set to false if `CreateState`, is invoked.
		var existingStateFound = true;

		// Note: This incurs a closure, but should only be called a few times at most during application
		// startup, so we are not too concerned with the performance impact.
		var state = ElasticOpenTelemetry.BuilderStateTable.GetValue(builder, _ =>
			CreateState(builder, builderName, services, ref options, ref existingStateFound));

		components = state.Components;

		Debug.Assert(components is not null);

		ValidateGlobalCallCount(methodName, builderName, options, components, callCount);

		// This allows us to track the number of times a specific instance of a builder is configured.
		// We expect each builder to be configured at most once and log a warning if multiple invocations
		// are detected.
		state.IncrementUseElasticDefaults();

		if (state.UseElasticDefaultsCounter > 1)
			components.Logger.LogWarning("The `{MethodName}` method has been called {UseElasticDefaultsCount} " +
				"times on the same `{BuilderType}` (instance: {BuilderInstanceId}). This method is " +
				"expected to be invoked a maximum of one time.", methodName,
				state.UseElasticDefaultsCounter, builderName, state.InstanceIdentifier);

		if (existingStateFound && state.BootstrapInfo.Succeeded)
		{
			// If `UseElasticDefaults` is invoked more than once on the same builder instance,
			// we reuse the same components and skip the configure action.

			components.Logger.LogTrace("Existing components have been found for the current {Builder} " +
				"instance (instance: {BuilderInstanceId}) and will be reused.", builderName,
				state.InstanceIdentifier);

			return true;
		}

		configure(builder, components);

		if (state.BootstrapInfo.Failed)
		{
			components.Logger.LogError("Unable to bootstrap EDOT.");

			// Remove the builder from the state table so that if a later call attempts to configure
			// it, we try again.
			ElasticOpenTelemetry.BuilderStateTable.Remove(builder);
		}

		return state.BootstrapInfo.Succeeded;

		static BuilderState CreateState(T builder, string builderName, IServiceCollection? services,
			[NotNull] ref CompositeElasticOpenTelemetryOptions? options, ref bool existingStateFound)
		{
			existingStateFound = false;

			var instanceId = Guid.NewGuid(); // Used in logging to track duplicate calls to the same builder

			// We can't log to the file here as we don't yet have any bootstrapped components.
			// Therefore, this message will only appear if the consumer provides an additional logger.
			// This is fine as it's a trace level message for advanced debugging.
			options?.AdditionalLogger?.LogTrace($"No existing {nameof(ElasticOpenTelemetryComponents)} have " +
				"been found for the current {Builder} (instance: {BuilderInstanceId}, hash: {BuilderHashCode}).",
				builderName, instanceId, builder.GetHashCode());

			options ??= CompositeElasticOpenTelemetryOptions.DefaultOptions;

			var bootStrapInfo = ElasticOpenTelemetry.TryBootstrap(options, services, out var components);
			var builderState = new BuilderState(bootStrapInfo, components, instanceId);

			components.Logger?.LogTrace("Storing state for the current {Builder} " +
				"instance (instance: {BuilderInstanceId}, hash: {BuilderHashCode}).",
				builderName, builderState.InstanceIdentifier, builder.GetHashCode());

			return builderState;
		}

		static void ValidateGlobalCallCount(string methodName, string builderName, CompositeElasticOpenTelemetryOptions? options,
			ElasticOpenTelemetryComponents? components, int callCount)
		{
			if (callCount > 1)
			{
				var logger = components is not null ? components.Logger : options?.AdditionalLogger;
				logger?.LogWarning("The `{MethodName}` method has been called {UseElasticDefaultsCount} " +
					"times across all {Builder} instances. This method is generally expected to be invoked " +
					"once. Consider reviewing the usage at the callsite(s).", methodName,
					callCount, builderName);
			}
		}
	}
}
