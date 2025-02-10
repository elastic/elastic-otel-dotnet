// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using Elastic.OpenTelemetry.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Elastic.OpenTelemetry.Core;

internal static class SignalBuilder
{
	public static bool Configure<T>(
		string methodName,
		string builderName,
		T builder,
		GlobalProviderBuilderState globalProviderBuilderState,
		CompositeElasticOpenTelemetryOptions? options,
		IServiceCollection? services,
		Action<T, CompositeElasticOpenTelemetryOptions, ElasticOpenTelemetryComponents> configure,
		[NotNullWhen(true)] ref ElasticOpenTelemetryComponents? components) where T : class
	{
		components = null;

		var callCount = globalProviderBuilderState.IncrementUseElasticDefaults();

		var existingStateFound = true;

		var state = ElasticOpenTelemetry.BuilderStateTable.GetValue(builder, _ => CreateState(builder, builderName, services, ref options, ref existingStateFound));

		if (existingStateFound && state.BootstrapInfo.Success)
		{
			components = state.Components;

			Debug.Assert(components is not null);

			components.Logger.LogTrace("Existing components have been found for the current {Builder} " +
				"instance (instance: {BuilderInstanceId}) and will be reused.", builderName,
				state.InstanceIdentifier);

			state.IncrementUseElasticDefaults();

			if (state.UseElasticDefaultsCounter > 1)
				components.Logger.LogWarning("The `{MethodName}` method has been called {UseElasticDefaultsCount} " +
					"times on the same `{BuilderType}` (instance: {BuilderInstanceId}). This method is " +
					"expected to be invoked a maximum of one time.", methodName,
					state.UseElasticDefaultsCounter, builderName, state.InstanceIdentifier);

			return true;
		}

		components = state.Components;

		if (callCount > 1)
		{
			var logger = components is not null ? components.Logger : options?.AdditionalLogger;
			logger?.LogWarning("The `{MethodName}` method has been called {UseElasticDefaultsCount} " +
				"times across all {Builder} instances. This method is generally expected to be invoked " +
				"once. Consider reviewing the usage at the callsite(s).", methodName,
				callCount, builderName);
		}
		else if (components is not null)
			configure(builder, options ?? components.Options, components);

		if (!state.BootstrapInfo.Success)
		{
			options?.AdditionalLogger?.LogError("Unable to bootstrap EDOT.");
			ElasticOpenTelemetry.BuilderStateTable.Remove(builder);
		}

		return state.BootstrapInfo.Success;

		static BuilderState CreateState(T builder, string builderName, IServiceCollection? services, ref CompositeElasticOpenTelemetryOptions? options, ref bool existingStateFound)
		{
			existingStateFound = false;

			var instanceId = Guid.NewGuid();

			// We can't log to the file here as we don't yet have any bootstrapped components.
			// Therefore, this message will only appear if the consumer provides an additional logger.
			// This is fine as it's a trace level message for advanced debugging.
			options?.AdditionalLogger?.LogTrace($"No existing {nameof(ElasticOpenTelemetryComponents)} have " +
				"been found for the current {Builder} (instance: {BuilderInstanceId}, hash: {BuilderHashCode}).",
				builderName, instanceId, builder.GetHashCode());

			options ??= CompositeElasticOpenTelemetryOptions.DefaultOptions;

			var bootStrapInfo = options is null
				? ElasticOpenTelemetry.TryBootstrap(services, out var components)
				: ElasticOpenTelemetry.TryBootstrap(options, services, out components);

			var builderState = new BuilderState(bootStrapInfo, components, instanceId);

			var logger = components is not null ? components.Logger : options?.AdditionalLogger;

			logger?.LogTrace("Storing state for the current {Builder} " +
				"instance (instance: {BuilderInstanceId}, hash: {BuilderHashCode}).",
				builderName, builderState.InstanceIdentifier, builder.GetHashCode());

			return builderState;
		}
	}
}
