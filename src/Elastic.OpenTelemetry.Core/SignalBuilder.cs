// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
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
///  <item><see cref="ResourceBuilder"/></item>
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
		CompositeElasticOpenTelemetryOptions? options)
	{
		if (components?.Logger is not null)
			return components.Logger;

		if (options is not null)
		{
			var deferredLogger = DeferredLogger.GetOrCreate(options);

			if (deferredLogger is NullLogger && options.AdditionalLogger is not null)
				return options.AdditionalLogger;
		}

		return NullLogger.Instance;
	}

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

		if (BuilderStateTable.TryGetValue(builder, out builderState))
			return builderState.Components.Logger;

		return GetLogger(components, options);
	}

	/// <summary>
	/// This overload is needed to handle scenarios where we don't yet have a builder state and context.
	/// </summary>
	internal static T WithElasticDefaults<T>(
		T builder,
		CompositeElasticOpenTelemetryOptions? options,
		ElasticOpenTelemetryComponents? components,
		IServiceCollection? services,
		in BuilderOptions<T> builderOptions,
		Action<BuilderContext<T>> configureBuilder) where T : class
	{
		// FullName may return null so we fallback to Name when required.
		var providerBuilderName = builder.GetType().FullName ?? builder.GetType().Name;

		if (BuilderStateTable.TryGetValue(builder, out var existingBuilderState))
		{
			return HandleExistingBuilderState(builder, providerBuilderName, existingBuilderState);
		}

		var logger = GetLogger(components, options);
		var builderInstanceId = Guid.NewGuid().ToString(); // Used in logging to track duplicate calls to the same builder

		// When dealing with a signal specific builder, we need to check if that signal is enabled.
		// If not, we can log and return early.
		if (builder is not IOpenTelemetryBuilder && builder is not ResourceBuilder)
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

			var builderContext = new BuilderContext<T>
			{
				Builder = builder,
				BuilderState = builderState,
				BuilderOptions = builderOptions,
				Services = services
			};

			configureBuilder(builderContext);

			components.Logger.LogStoringBuilderState(providerBuilderName, builderInstanceId);
			BuilderStateTable.Add(builder, builderState);
		}

		return builder;

		static T HandleExistingBuilderState(
			T builder,
			string providerBuilderName,
			BuilderState builderState)
		{
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
