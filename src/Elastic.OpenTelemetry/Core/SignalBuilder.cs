// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Diagnostics;
using Elastic.OpenTelemetry.Instrumentation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Elastic.OpenTelemetry.Core;

/// <summary>
/// Provides static helper methods which centralise provider builder logic used when registering EDOT
/// defaults on the various builders.
/// </summary>
internal static class SignalBuilder
{
	/// <summary>
	/// Returns the most relevant <see cref="ILogger"/> for builder extension methods to use.
	/// </summary>
	/// <param name="components"></param>
	/// <param name="options"></param>
	/// <returns></returns>
	public static ILogger GetLogger(ElasticOpenTelemetryComponents? components, CompositeElasticOpenTelemetryOptions? options) =>
		components?.Logger ?? options?.AdditionalLogger ?? NullLogger.Instance;

	public static bool IsSignalEnabled(ElasticOpenTelemetryComponents? components, CompositeElasticOpenTelemetryOptions? options,
		Signals signalToCheck, string providerBuilderName, ILogger logger)
	{
		var configuredSignals = components?.Options.Signals ?? options?.Signals ?? Signals.All;
		if (!configuredSignals.HasFlagFast(signalToCheck))
		{
			logger.LogSignalDisabled(signalToCheck.ToString().ToLower(), providerBuilderName);
			return false;
		}

		return true;
	}

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
		var callCount = globalProviderBuilderState.IncrementWithElasticDefaults();

		// If we are provided with options and components, we can avoid attempting to bootstrap again.
		// This scenario occurs if for example `AddElasticOpenTelemetry` is called multipled times
		// on the same `IServiceCollection`. In this case, a new `OpenTelemetryBuilder` would be
		// created (inside the SDK) for each call to `AddOpenTelemetry`, so the `BuilderState`, is
		// not useful. `TryBootstrap` would be eventually reuse cached components registered
		// against the `IServiceCollection`, but we can still be more efficient to avoid calling that
		// code in this particular case by shortcutting and returning early.
		if (options is not null && components is not null)
		{
			configure(builder, components);
			return true;
		}

		// This will later be set to false if `CreateState`, is invoked.
		var existingStateFound = true;

		// Note: This incurs a closure, but it should only be called a few times at most during application
		// startup, so we are not too concerned with the performance impact.
		var state = ElasticOpenTelemetry.BuilderStateTable.GetValue(builder, _ =>
			CreateState(builder, builderName, services, ref options, ref existingStateFound));

		// At this point, we either have cached components for the builder or a new instance
		// created by the `CreateState` method.

		Debug.Assert(state.Components is not null);

		components = state.Components;

		ValidateGlobalCallCount(methodName, builderName, options, components, callCount);

		// This allows us to track the number of times a specific instance of a builder is configured.
		// We expect each builder to be configured at most once and log a warning if multiple invocations
		// are detected.
		state.IncrementWithElasticDefaults();

		if (state.WithElasticDefaultsCounter > 1)
			components.Logger.LogWarning("The `{MethodName}` method has been called {WithElasticDefaultsCount} " +
				"times on the same `{BuilderType}` (instance: {BuilderInstanceId}). This method is " +
				"expected to be invoked a maximum of one time.", methodName,
				state.WithElasticDefaultsCounter, builderName, state.InstanceIdentifier);

		if (existingStateFound && state.BootstrapInfo.Succeeded)
		{
			// If `WithElasticDefaults` is invoked more than once on the same builder instance,
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
				logger?.LogWarning("The `{MethodName}` method has been called {WithElasticDefaultsCount} " +
					"times across all {Builder} instances. This method is generally expected to be invoked " +
					"once. Consider reviewing the usage at the callsite(s).", methodName,
					callCount, builderName);
			}
		}
	}

	/// <summary>
	/// Identifies whether a specific instrumentation assembly is present alongside the executing application.
	/// </summary>
	public static bool InstrumentationAssemblyExists(string assemblyName)
	{
		var assemblyLocation = Path.GetDirectoryName(AppContext.BaseDirectory);

		if (string.IsNullOrEmpty(assemblyLocation))
			return false;

		return File.Exists(Path.Combine(assemblyLocation, assemblyName));
	}

	[RequiresUnreferencedCode("Accesses assemblies and methods dynamically using refelction. This is by design and cannot be made trim compatible.")]
	public static void AddInstrumentationViaReflection<T>(T builder, ElasticOpenTelemetryComponents components, ReadOnlySpan<InstrumentationAssemblyInfo> assemblyInfos)
			where T : class
	{
		if (components.Options.SkipInstrumentationAssemblyScanning)
			return;

		var logger = components.Logger;
		var builderTypeName = builder.GetType().Name;
		var assemblyLocation = AppContext.BaseDirectory;

		if (!string.IsNullOrEmpty(assemblyLocation))
		{
			foreach (var assemblyInfo in assemblyInfos)
			{
				try
				{
					var assemblyPath = Path.Combine(assemblyLocation, assemblyInfo.Filename);
					if (File.Exists(assemblyPath))
					{
						logger.LogLocatedInstrumentationAssembly(assemblyInfo.Filename, assemblyLocation);

						var assembly = Assembly.LoadFrom(assemblyPath);
						var type = assembly.GetType(assemblyInfo.FullyQualifiedType);

						if (type is null)
						{
							logger.LogWarning("Unable to find {FullyQualifiedTypeName} in {AssemblyFullName}.", assemblyInfo.FullyQualifiedType, assembly.FullName);
							continue;
						}

						var methodInfo = type.GetMethod(assemblyInfo.InstrumentationMethod, BindingFlags.Static | BindingFlags.Public,
							Type.DefaultBinder, [typeof(T)], null);

						if (methodInfo is null)
						{
							logger.LogWarning("Unable to find the {TypeName}.{Method} extension method in {AssemblyFullName}.",
								assemblyInfo.FullyQualifiedType, assemblyInfo.InstrumentationMethod, assembly.FullName);
							continue;
						}

						methodInfo.Invoke(null, [builder]); // Invoke the extension method to register the instrumentation with the builder.

						logger.LogAddedInstrumentation(assemblyInfo.Name, builderTypeName);
					}
				}
				catch (Exception ex)
				{
					logger.LogError(ex, "Failed to dynamically enable {InstrumentationName} on {Provider}.", assemblyInfo.Name, builderTypeName);
				}
			}
		}
		else
		{
			logger.LogWarning("The result of `AppContext.BaseDirectory` was null or empty. Unable to perform instrumentation assembly scanning.");
		}
	}
}
