// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

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
	private static readonly Lock Lock = new();

	/// <summary>
	/// Returns the most relevant <see cref="ILogger"/> for builder extension methods to use.
	/// </summary>
	/// <param name="components"></param>
	/// <param name="options"></param>
	/// <returns></returns>
	public static ILogger GetLogger(ElasticOpenTelemetryComponents? components, CompositeElasticOpenTelemetryOptions? options) =>
		components?.Logger ?? options?.AdditionalLogger ?? NullLogger.Instance;

	public static string GetBuilderIdentifier<T>(this T builder) where T : class =>
		!ElasticOpenTelemetry.BuilderStateTable.TryGetValue(builder, out var state)
			? builder.GetHashCode().ToString()
			: $"{state.InstanceIdentifier}:{builder.GetHashCode()}";

	public static void IncrementAndVerifyCallCount(string providerBuilderName, ILogger logger, BuilderState builderState)
	{
		// This allows us to track the number of times a specific instance of a builder is configured.
		// We expect each builder to be configured at most once and log a warning if multiple invocations
		// are detected.
		builderState.IncrementWithElasticDefaults();

		if (builderState.WithElasticDefaultsCounter > 1)
			logger.LogWarning("The `WithElasticDefaults` method has been called {WithElasticDefaultsCount} " +
				"times on the same `{BuilderType}` (instance: {BuilderInstanceId}). This method is " +
				"expected to be invoked a maximum of one time.", builderState.WithElasticDefaultsCounter, providerBuilderName,
				builderState.InstanceIdentifier);
	}

	public static T WithElasticDefaults<T>(
		T builder,
		Signals signal,
		CompositeElasticOpenTelemetryOptions? options,
		ElasticOpenTelemetryComponents? components,
		IServiceCollection? services,
		Action<T, BuilderState, IServiceCollection?> configure) where T : class
	{
		var providerBuilderName = builder.GetType().Name;
		var logger = GetLogger(components, options);

		try
		{
			// If the signal is disabled via configuration we skip any potential bootstrapping.
			var configuredSignals = components?.Options.Signals ?? options?.Signals ?? Signals.All;
			if (!configuredSignals.HasFlagFast(signal))
			{
				var builderInstanceId = ElasticOpenTelemetry.BuilderStateTable.TryGetValue(builder, out var builderState) ? builderState.InstanceIdentifier : "<unknown>";
				logger.LogSignalDisabled(signal.ToString().ToLower(), providerBuilderName, builderInstanceId);
				return builder;
			}

			WithElasticDefaults(builder, options, components, services, configure);
		}
		catch (Exception ex)
		{
			var signalNameForLogging = signal.ToStringFast().ToLowerInvariant();
			logger.LogError(new EventId(501, "BuilderDefaultsFailed"), ex, "Failed to fully register EDOT .NET " +
				"{Signal} defaults on the {ProviderBuilderName}.", signalNameForLogging, providerBuilderName);
		}

		return builder;
	}

	public static T WithElasticDefaults<T>(
		T builder,
		CompositeElasticOpenTelemetryOptions? options,
		ElasticOpenTelemetryComponents? components,
		IServiceCollection? services,
		Action<T, BuilderState, IServiceCollection?> configure) where T : class
	{
		var builderName = builder.GetType().Name;
		var logger = GetLogger(components, options);

		BuilderState builderState;

		// This should not be a hot path, so locking here is fine.
		using (var scope = Lock.EnterScope())
		{
			if (ElasticOpenTelemetry.BuilderStateTable.TryGetValue(builder, out builderState!))
			{
				logger.LogBuilderAlreadyConfigured(builderName, builderState.InstanceIdentifier);
				IncrementAndVerifyCallCount(builderName, logger, builderState);
				return builder;
			}

			var instanceId = Guid.NewGuid().ToString(); // Used in logging to track duplicate calls to the same builder

			// We can't log to the file here as we don't yet have any bootstrapped components.
			// Therefore, this message will only appear if the consumer provides an additional logger.
			// This is fine as it's a trace level message for advanced debugging.
			logger.LogNoExistingComponents(builderName, instanceId);

			options ??= CompositeElasticOpenTelemetryOptions.DefaultOptions;

			components = ElasticOpenTelemetry.Bootstrap(options, services);
			builderState = new BuilderState(components, instanceId);

			components.Logger.LogStoringBuilderState(builderName, builderState.InstanceIdentifier);

			ElasticOpenTelemetry.BuilderStateTable.Add(builder, builderState);
		}

		configure(builder, builderState, services);
		return builder;
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
	public static void AddInstrumentationViaReflection<T>(T builder, ElasticOpenTelemetryComponents components, ReadOnlySpan<InstrumentationAssemblyInfo> assemblyInfos, string builderInstanceId)
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
	public static void AddInstrumentationViaReflection<T>(T builder, ILogger logger, ReadOnlySpan<InstrumentationAssemblyInfo> assemblyInfos, string builderInstanceId)
		where T : class
	{
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
							logger.LogUnableToFindTypeWarning(assemblyInfo.FullyQualifiedType, assembly.FullName ?? "UNKNOWN");
							continue;
						}

						var methodInfo = type.GetMethod(assemblyInfo.InstrumentationMethod, BindingFlags.Static | BindingFlags.Public,
							Type.DefaultBinder, [typeof(T)], null);

						if (methodInfo is null)
						{
							logger.LogUnableToFindMethodWarning(assemblyInfo.FullyQualifiedType, assemblyInfo.InstrumentationMethod,
								assembly.FullName ?? "UNKNOWN");
							continue;
						}

						methodInfo.Invoke(null, [builder]); // Invoke the extension method to register the instrumentation with the builder.

						logger.LogAddedInstrumentationViaReflection(assemblyInfo.Name, builderTypeName, builderInstanceId);
					}
				}
				catch (Exception ex)
				{
					logger.LogError(new EventId(502, "DynamicInstrumentaionFailed"), ex, "Failed to dynamically enable " +
						"{InstrumentationName} on {Provider}.", assemblyInfo.Name, builderTypeName);
				}
			}
		}
		else
		{
			logger.LogBaseDirectoryWarning();
		}
	}
}
