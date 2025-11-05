// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Elastic.OpenTelemetry.Core;

/// <summary>
/// Provides static helper methods which centralise provider builder logic used when registering EDOT
/// defaults on the various builders.
/// </summary>
/// <remarks>
/// This class is used internally by the various builder extension methods to centralise common logic.
/// <br/>
/// It is currently used when registering EDOT .NET defaults on the following builders:
/// <list type="bullet">
///  <item><see cref="IOpenTelemetryBuilder"/></item>
///  <item><see cref="TracerProviderBuilder"/></item>
///  <item><see cref="MeterProviderBuilder"/></item>
///  <item><see cref="LoggerProviderBuilder"/></item>
///</list>
/// </remarks>
internal static class SignalBuilder
{
#pragma warning disable IDE0028 // Simplify collection initialization
	private static readonly ConditionalWeakTable<object, BuilderState> BuilderStateTable = new();
#pragma warning restore IDE0028 // Simplify collection initialization

	private static readonly Lock Lock = new();

	/// <summary>
	/// Returns the most relevant <see cref="ILogger"/> for builder extension methods to use.
	/// </summary>
	internal static ILogger GetLogger(
		ElasticOpenTelemetryComponents? components,
		CompositeElasticOpenTelemetryOptions? options) =>
			components?.Logger ?? options?.AdditionalLogger ?? NullLogger.Instance;

	/// <summary>
	/// Returns the most relevant <see cref="ILogger"/> for builder extension methods to use.
	/// </summary>
	internal static ILogger GetLogger<T>(
		T builder,
		ElasticOpenTelemetryComponents? components,
		CompositeElasticOpenTelemetryOptions? options,
		BuilderState? builderState) where T : class
	{
		if (builderState is not null)
			return builderState.Components.Logger;

		var logger = components?.Logger ?? options?.AdditionalLogger ?? NullLogger.Instance;

		if (BuilderStateTable.TryGetValue(builder, out builderState))
			logger = builderState.Components.Logger;

		return logger;
	}

	//internal static T WithElasticDefaults<T>(
	//	T builder,
	//	Signals signal,
	//	CompositeElasticOpenTelemetryOptions? options,
	//	ElasticOpenTelemetryComponents? components,
	//	IServiceCollection? services,
	//	BuilderOptions<T> builderOptions,
	//	Action<BuilderContext<T>> configure) where T : class
	//{
	//	var providerBuilderName = builder.GetType().Name;
	//	var logger = GetLogger(components, options);

	//	BuilderState? builderState = null;

	//	try
	//	{
	//		var builderInstanceId = "<unknown>";

	//		if (BuilderStateTable.TryGetValue(builder, out var existingBuilderState))
	//		{
	//			builderState = existingBuilderState;
	//			builderInstanceId = existingBuilderState.InstanceIdentifier;
	//		}

	//		if (builderState is not null)
	//			logger = builderState.Components.Logger;

	//		// If the signal is disabled via configuration we skip any potential bootstrapping.
	//		var configuredSignals = components?.Options.Signals ?? options?.Signals ?? Signals.All;
	//		if (!configuredSignals.HasFlagFast(signal))
	//		{
	//			logger.LogSignalDisabled(signal.ToString().ToLower(), providerBuilderName, builderInstanceId);
	//			return builder;
	//		}

	//		return WithElasticDefaults(builder, options, components, services, builderOptions, configure);
	//	}
	//	catch (Exception ex)
	//	{
	//		var signalNameForLogging = signal.ToStringFast().ToLowerInvariant();
	//		logger.LogError(new EventId(501, "BuilderDefaultsFailed"), ex, "Failed to fully register EDOT .NET " +
	//			"{Signal} defaults on the {ProviderBuilderName}.", signalNameForLogging, providerBuilderName);
	//	}

	//	return builder;
	//}

	internal static T WithElasticDefaults<T>(
		BuilderContext<T> builderContext,
		Action<BuilderContext<T>> configureBuilder) where T : class
	{
		var builder = builderContext.Builder;
		var components = builderContext.BuilderState.Components;
		var options = components.Options;
		var services = builderContext.Services;
		var providerBuilderName = builder.GetType().Name;
		var logger = GetLogger(components, options);
		var builderInstanceId = Guid.NewGuid().ToString(); // Used in logging to track duplicate calls to the same builder

		try
		{
			if (BuilderStateTable.TryGetValue(builder, out var existingBuilderState))
			{
				return HandleExistingBuilderState(builder, providerBuilderName, existingBuilderState);
			}

			if (builder is not IOpenTelemetryBuilder)
			{
				var configuredSignals = components?.Options.Signals ?? options?.Signals ?? Signals.All;

				var signal = Signals.None;
				switch (builder)
				{
					case TracerProviderBuilder:
						signal = Signals.Traces;
						break;
					case MeterProviderBuilder:
						signal = Signals.Metrics;
						break;
					case LoggerProviderBuilder:
						signal = Signals.Logs;
						break;
				}

				if (configuredSignals.HasFlagFast(signal) is false)
				{
					logger.LogSignalDisabled(signal.ToString().ToLower(), providerBuilderName, builderInstanceId);
					return builder;
				}
			}

			// This should not be a hot path, so locking here is reasonable.
			using (var scope = Lock.EnterScope())
			{
				// Double check after acquiring the lock.
				if (BuilderStateTable.TryGetValue(builder, out existingBuilderState))
				{
					return HandleExistingBuilderState(builder, providerBuilderName, existingBuilderState);
				}

				// We can't log to the file here as we don't yet have any bootstrapped components.
				// Therefore, this message will only appear if the consumer provides an additional logger.
				// This is fine as it's a trace level message for advanced debugging.
				logger.LogNoExistingComponents(providerBuilderName, builderInstanceId);

				options ??= CompositeElasticOpenTelemetryOptions.DefaultOptions;

				// This will check for any existing components on the IServiceCollection and reuse them if found.
				// It would also attempt to used a shared components instance if available.
				// If neither are available, it will create a new instance.
				components = ElasticOpenTelemetry.Bootstrap(options, services);
				var builderState = new BuilderState(components, builderInstanceId);

				configureBuilder(builderContext);

				components.Logger.LogStoringBuilderState(providerBuilderName, builderInstanceId);
				BuilderStateTable.Add(builder, builderState);
			}
		}
		catch (Exception ex)
		{
			logger.LogError(new EventId(502, "BuilderDefaultsFailed"), ex, "Failed to fully register EDOT .NET " +
				"defaults on the {ProviderBuilderName}.", providerBuilderName);
		}

		return builder;

		static T HandleExistingBuilderState(
			T builder,
			string providerBuilderName,
			BuilderState builderState)
		{
			var builderInstanceId = builderState.InstanceIdentifier;
			var logger = builderState.Components.Logger;
			logger.LogBuilderAlreadyConfigured(providerBuilderName, builderState.InstanceIdentifier);

			// This allows us to track the number of times a specific instance of a builder is configured.
			// We expect each builder to be configured at most once and log a warning if multiple invocations
			// are detected.
			builderState.IncrementWithElasticDefaults();

			if (builderState.WithElasticDefaultsCounter > 1)
				// NOTE: Log using the logger from the existing builder state.
				builderState.Components.Logger.LogWarning("The `WithElasticDefaults` method has been called {WithElasticDefaultsCount} " +
					"times on the same `{BuilderType}` (instance: {BuilderInstanceId}). This method is " +
					"expected to be invoked a maximum of one time.", builderState.WithElasticDefaultsCounter, providerBuilderName,
					builderState.InstanceIdentifier);

			return builder;
		}
	}

	/// <summary>
	/// Identifies whether a specific instrumentation assembly is present alongside the executing application.
	/// </summary>
	internal static bool InstrumentationAssemblyExists(string assemblyName)
	{
		var assemblyLocation = Path.GetDirectoryName(AppContext.BaseDirectory);

		if (string.IsNullOrEmpty(assemblyLocation))
			return false;

		return File.Exists(Path.Combine(assemblyLocation, assemblyName));
	}

	[RequiresUnreferencedCode("Accesses assemblies and methods dynamically using refelction. This is by design and cannot be made trim compatible.")]
	internal static void AddInstrumentationViaReflection<T>(T builder, ElasticOpenTelemetryComponents components, ReadOnlySpan<InstrumentationAssemblyInfo> assemblyInfos, string builderInstanceId)
			where T : class
	{
		if (components.Options.SkipInstrumentationAssemblyScanning)
		{
			components.Logger.LogSkippingAssemblyScanning(builder.GetType().Name, builderInstanceId);
			return;
		}

		AddInstrumentationViaReflection(builder, components.Logger, assemblyInfos, builderInstanceId);
	}

	[RequiresUnreferencedCode("Accesses assemblies and methods dynamically using refelction. This is by design and cannot be made trim compatible.")]
	internal static void AddInstrumentationViaReflection<T>(T builder, ILogger logger, ReadOnlySpan<InstrumentationAssemblyInfo> assemblyInfos, string builderInstanceId)
		where T : class
	{
		var builderTypeName = builder.GetType().Name;

		foreach (var assemblyInfo in assemblyInfos)
		{
			try
			{
				var type = Type.GetType($"{assemblyInfo.FullyQualifiedType}, {assemblyInfo.AssemblyName}");

				if (type is null)
				{
					logger.LogUnableToFindType(assemblyInfo.FullyQualifiedType, assemblyInfo.AssemblyName);
					continue;
				}

				var methodInfo = type.GetMethod(assemblyInfo.InstrumentationMethod, BindingFlags.Static | BindingFlags.Public,
					Type.DefaultBinder, [typeof(T)], null);

				if (methodInfo is null)
				{
					logger.LogUnableToFindMethodWarning(assemblyInfo.FullyQualifiedType, assemblyInfo.InstrumentationMethod,
						assemblyInfo.AssemblyName);
					continue;
				}

				methodInfo.Invoke(null, [builder]); // Invoke the extension method to register the instrumentation with the builder.

				if (builderTypeName.StartsWith("ResourceBuilder"))
				{
					logger.LogAddedResourceDetectorViaReflection(assemblyInfo.Name, builderTypeName, builderInstanceId);
				}
				else
				{
					logger.LogAddedInstrumentationViaReflection(assemblyInfo.Name, builderTypeName, builderInstanceId);
				}
			}
			catch (Exception ex)
			{
				logger.LogError(new EventId(503, "DynamicInstrumentaionFailed"), ex, "Failed to dynamically enable " +
					"{InstrumentationName} on {Provider}.", assemblyInfo.Name, builderTypeName);
			}
		}
	}
}
