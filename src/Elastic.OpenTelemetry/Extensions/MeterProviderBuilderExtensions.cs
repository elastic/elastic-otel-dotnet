// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Elastic.OpenTelemetry;
using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Core;
using Elastic.OpenTelemetry.Diagnostics;
using Elastic.OpenTelemetry.Instrumentation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry.Trace;

// Matching namespace with MeterProviderBuilder
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace OpenTelemetry.Metrics;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods for <see cref="MeterProviderBuilder"/> used to register
/// the Elastic Distribution of OpenTelemetry (EDOT) defaults.
/// </summary>
public static class MeterProviderBuilderExtensions
{
	private static readonly GlobalProviderBuilderState GlobalMeterProviderBuilderState = new();

	/// <summary>
	/// Use Elastic Distribution of OpenTelemetry .NET defaults for <see cref="MeterProviderBuilder"/>.
	/// </summary>
	/// <remarks>
	/// Calling this method is not neccesary if <see cref="OpenTelemetryBuilderExtensions.WithElasticDefaults(IOpenTelemetryBuilder)"/>
	/// has been called previously as that automatically adds the defaults for all signals.
	/// </remarks>
	/// <param name="builder">The <see cref="MeterProviderBuilder"/> to configure.</param>
	/// <returns>The <see cref="MeterProviderBuilder"/> for chaining configuration.</returns>
	public static MeterProviderBuilder WithElasticDefaults(this MeterProviderBuilder builder) =>
		WithElasticDefaultsCore(builder, null, null);

	/// <summary>
	/// <inheritdoc cref="WithElasticDefaults(MeterProviderBuilder)" />
	/// </summary>
	/// <remarks><inheritdoc cref="WithElasticDefaults(MeterProviderBuilder)" /></remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(MeterProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="skipOtlpExporter">When registering Elastic defaults, skip automatic registration of the OTLP exporter for metrics.</param>
	/// <returns><inheritdoc cref="WithElasticDefaults(MeterProviderBuilder)" /></returns>
	public static MeterProviderBuilder WithElasticDefaults(this MeterProviderBuilder builder, bool skipOtlpExporter) =>
		WithElasticDefaultsCore(builder, skipOtlpExporter ? CompositeElasticOpenTelemetryOptions.SkipOtlpOptions : null, null);

	/// <summary>
	/// <inheritdoc cref="WithElasticDefaults(MeterProviderBuilder)" />
	/// </summary>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(MeterProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="options"><see cref="ElasticOpenTelemetryOptions"/> used to configure the Elastic Distribution of OpenTelemetry (EDOT) for .NET.</param>
	/// <returns><inheritdoc cref="WithElasticDefaults(MeterProviderBuilder)" /></returns>
	public static MeterProviderBuilder WithElasticDefaults(this MeterProviderBuilder builder, ElasticOpenTelemetryOptions options)
	{
#if NET
		ArgumentNullException.ThrowIfNull(options);
#else
		if (options is null)
			throw new ArgumentNullException(nameof(options));
#endif

		return WithElasticDefaultsCore(builder, new(options), null);
	}

	/// <summary>
	/// <inheritdoc cref="WithElasticDefaults(MeterProviderBuilder)" />
	/// </summary>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(MeterProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configuration">An <see cref="IConfiguration"/> instance from which to load the Elastic Distribution of
	/// OpenTelemetry (EDOT) options.</param>
	/// <returns><inheritdoc cref="WithElasticDefaults(MeterProviderBuilder)" /></returns>
	public static MeterProviderBuilder WithElasticDefaults(this MeterProviderBuilder builder, IConfiguration configuration)
	{
#if NET
		ArgumentNullException.ThrowIfNull(configuration);
#else
		if (configuration is null)
			throw new ArgumentNullException(nameof(configuration));
#endif
		return WithElasticDefaultsCore(builder, new(configuration), null);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static MeterProviderBuilder WithElasticDefaults(
		this MeterProviderBuilder builder,
		ElasticOpenTelemetryComponents components) =>
			WithElasticDefaultsCore(builder, components.Options, components, null);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static MeterProviderBuilder WithElasticDefaults(
		this MeterProviderBuilder builder,
		ElasticOpenTelemetryComponents components,
		IServiceCollection serviceCollection) =>
			WithElasticDefaultsCore(builder, components.Options, components, serviceCollection);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static MeterProviderBuilder WithElasticDefaults(
		this MeterProviderBuilder builder,
		IConfiguration configuration,
		IServiceCollection serviceCollection) =>
			WithElasticDefaultsCore(builder, new(configuration), null, serviceCollection);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static MeterProviderBuilder WithElasticDefaults(
		this MeterProviderBuilder builder,
		IServiceCollection serviceCollection) =>
			WithElasticDefaultsCore(builder, null, null, serviceCollection);

	[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "The calls to `AddSqlClientInstrumentation` and `AssemblyScanning.AddInstrumentationViaReflection` " +
			"are guarded by a RuntimeFeature.IsDynamicCodeSupported` check and therefore this method is safe to call in AoT scenarios.")]
	internal static MeterProviderBuilder WithElasticDefaultsCore(
		this MeterProviderBuilder builder,
		CompositeElasticOpenTelemetryOptions? options,
		ElasticOpenTelemetryComponents? components,
		IServiceCollection? services = null)
	{
		const string providerBuilderName = nameof(MeterProviderBuilder);

		var logger = SignalBuilder.GetLogger(components, options);

		// If the signal is disabled via configuration we skip any potential bootstrapping.
		if (!SignalBuilder.IsSignalEnabled(components, options, Signals.Metrics, providerBuilderName, logger))
			return builder;

		try
		{
			if (!SignalBuilder.ConfigureBuilder(nameof(WithElasticDefaults), providerBuilderName, builder,
				GlobalMeterProviderBuilderState, options, services, ConfigureBuilder, ref components))
			{
				logger = components?.Logger ?? options?.AdditionalLogger ?? NullLogger.Instance; // Update the logger we should use from the ref-returned components.
				logger.UnableToConfigureLoggingDefaultsError(providerBuilderName);
				return builder;
			}
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to fully register EDOT .NET meter defaults for {ProviderBuilderType}.", providerBuilderName);
		}

		return builder;

		static void ConfigureBuilder(MeterProviderBuilder builder, ElasticOpenTelemetryComponents components)
		{
			builder.ConfigureResource(r => r.AddElasticDistroAttributes());

#if NET9_0_OR_GREATER
			// On .NET 9, the contrib HTTP instrumentation is no longer required. If the dependency exists,
			// it will be registered via the reflection-based assembly scanning.
			if (SignalBuilder.InstrumentationAssemblyExists("OpenTelemetry.Instrumentation.Http.dll"))
			{
				components.Logger.LogHttpInstrumentationFound("metric");

				// For native AOT scenarios, the reflection-based assembly scanning will not run.
				// Therefore, we log a warning since no HTTP instrumentation will be automatically registered.
				// In this scenario, the consumer must register the contrib instrumentation manually, or
				// remove the dependency so that the native .NET 9 HTTP instrumentation source will be added
				// instead.
				if (!RuntimeFeature.IsDynamicCodeSupported)
					components.Logger.LogWarning("The OpenTelemetry.Instrumentation.Http.dll was found alongside the executing assembly. " +
						"When using Native AOT publishing on .NET, the metric instrumentation is not registered automatically. Either register it manually, " +
						"or remove the dependency so that the native `System.Net.Http` instrumentation (available in .NET 9) is observed instead.");
			}
			else
			{
				AddWithLogging(builder, components.Logger, "HTTP (via native instrumentation)", b => b.AddMeter("System.Net.Http"));
			}

			// On .NET 9, the contrib runtime instrumentation is no longer required. If the dependency exists,
			// it will be registered via the reflection-based assembly scanning.
			if (SignalBuilder.InstrumentationAssemblyExists("OpenTelemetry.Instrumentation.Runtime.dll"))
			{
				components.Logger.LogRuntimeInstrumentationFound();

				// For native AOT scenarios, the reflection-based assembly scanning will not run.
				// Therefore, we log a warning since no runtime metric instrumentation will be automatically registered.
				// In this scenario, the consumer must register the contrib instrumentation manually, or
				// remove the dependency so that the native .NET 9 runtime metric instrumentation source will be added
				// instead.
				if (!RuntimeFeature.IsDynamicCodeSupported)
					components.Logger.LogWarning("The OpenTelemetry.Instrumentation.Runtime.dll was found alongside the executing assembly. " +
						"When using Native AOT publishing on .NET, the metric instrumentation is not registered automatically. Either register it manually, " +
						"or remove the dependency so that the native `System.Runtime` instrumentation (available in .NET 9) is observed instead.");
			}
			else
			{
				AddWithLogging(builder, components.Logger, "Runtime", b => b.AddMeter("System.Runtime"));
			}
#else
			AddWithLogging(builder, components.Logger, "HTTP (via contrib instrumentation)", b => b.AddHttpClientInstrumentation());
			AddWithLogging(builder, components.Logger, "Runtime", b => b.AddRuntimeInstrumentation());
#endif

#if NET
			if (RuntimeFeature.IsDynamicCodeSupported)
#endif
			{
				SignalBuilder.AddInstrumentationViaReflection(builder, components, ContribMetricsInstrumentation.GetMetricsInstrumentationAssembliesInfo());
			}

			if (components.Options.SkipOtlpExporter)
			{
				components.Logger.LogSkippingOtlpExporter(nameof(Signals.Traces), nameof(MeterProviderBuilder));
			}
			else
			{
				builder.AddOtlpExporter();
			}

			components.Logger.LogConfiguredSignalProvider(nameof(Signals.Logs), nameof(MeterProviderBuilder));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static void AddWithLogging(MeterProviderBuilder builder, ILogger logger, string name, Action<MeterProviderBuilder> add)
		{
			add.Invoke(builder);
			logger.LogAddedInstrumentation(name, nameof(MeterProviderBuilder));
		}
	}

	// We use a different method here to ensure we don't cause a crash depending on instrumentation libraries which are not present.
	// We can't assume that any DLLs are available besides OpenTelemetry.dll, which auto-instrumentation includes.
	// The auto instrumentation enables a set of default instrumentation of it's own, so we rely on that.
	// In the future, we can assess if we should copy instrumentation DLLs into the autoinstrumentation zip file and enable them.
	internal static MeterProviderBuilder UseAutoInstrumentationElasticDefaults(this MeterProviderBuilder builder, ElasticOpenTelemetryComponents components)
	{
		Debug.Assert(components is not null, "Components should not be null when invoked from the auto instrumentation.");

		try
		{
			builder.ConfigureResource(r => r.AddElasticDistroAttributes());

			if (components.Options.SkipOtlpExporter)
			{
				components.Logger.LogSkippingOtlpExporter(nameof(Signals.Traces), nameof(TracerProviderBuilder));
			}
			else
			{
				builder.AddOtlpExporter();
			}

			components.Logger.LogConfiguredSignalProvider("Traces", nameof(TracerProviderBuilder));

			return builder;
		}
		catch (Exception ex)
		{
			components?.Logger?.LogError(ex, "Failed to register EDOT defaults for meter auto-instrumentation to the {Provider}.", nameof(TracerProviderBuilder));
		}

		return builder;
	}
}
