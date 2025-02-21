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

	// Note: This is defined as a static method and allocates the array each time.
	// This is intentional, as we expect this to be invoked once (or worst case, few times).
	// After initialisation, the array is no longer required and can be reclaimed by the GC.
	// This is likley to be overall more efficient for the common scenario as we don't keep
	// an object alive for the lifetime of the application.
	private static InstrumentationAssemblyInfo[] GetReflectionInstrumentationAssemblies() =>
	[
		new()
		{
			Name = "AspNetCore",
			Filename = "OpenTelemetry.Instrumentation.AspNetCore.dll",
			FullyQualifiedType = "OpenTelemetry.Metrics.AspNetCoreInstrumentationMeterProviderBuilderExtensions",
			InstrumentationMethod = "AddAspNetCoreInstrumentation"
		},
#if NET9_0_OR_GREATER
		// On .NET 9, we add the `System.Net.Http` source for native instrumentation, rather than referencing
		// the contrib instrumentation. However, if the consuming application has their own reference to
		// `OpenTelemetry.Instrumentation.Http`, then we use that since it signals the consumer prefers the
		// contrib instrumentation. Therefore, on .NET 9+ targets, we attempt to dynamically load the contrib
		// instrumentation, when available.
		new()
		{
			Name = "Http",
			Filename = "OpenTelemetry.Instrumentation.Http.dll",
			FullyQualifiedType = "OpenTelemetry.Metrics.HttpClientInstrumentationMeterProviderBuilderExtensions",
			InstrumentationMethod = "AddHttpClientInstrumentation"
		},
#endif
	];

	/// <summary>
	/// Use Elastic Distribution of OpenTelemetry .NET defaults for <see cref="MeterProviderBuilder"/>.
	/// </summary>
	/// <remarks>
	/// This is not neccesary if <see cref="OpenTelemetryBuilderExtensions.WithElasticDefaults(IOpenTelemetryBuilder)"/>
	/// has been called previously as that automatically adds the .
	/// </remarks>
	/// <param name="builder">The <see cref="MeterProviderBuilder"/> to configure.</param>
	/// <returns>The <see cref="MeterProviderBuilder"/> for chaining configuration.</returns>
	public static MeterProviderBuilder UseElasticDefaults(this MeterProviderBuilder builder) =>
		UseElasticDefaultsCore(builder, null, null);

	/// <summary>
	/// <inheritdoc cref="UseElasticDefaults(MeterProviderBuilder)" />
	/// </summary>
	/// <param name="builder"><inheritdoc cref="UseElasticDefaults(MeterProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="skipOtlpExporter">When registering Elastic defaults, skip automatic registration of the OTLP exporter for metrics.</param>
	/// <returns><inheritdoc cref="UseElasticDefaults(MeterProviderBuilder)" /></returns>
	public static MeterProviderBuilder UseElasticDefaults(this MeterProviderBuilder builder, bool skipOtlpExporter) =>
		UseElasticDefaultsCore(builder, skipOtlpExporter ? CompositeElasticOpenTelemetryOptions.SkipOtlpOptions : null, null);

	/// <summary>
	/// <inheritdoc cref="UseElasticDefaults(MeterProviderBuilder)" />
	/// </summary>
	/// <param name="builder"><inheritdoc cref="UseElasticDefaults(MeterProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="options"><see cref="ElasticOpenTelemetryOptions"/> used to configure the Elastic Distribution of OpenTelemetry (EDOT) for .NET.</param>
	/// <returns><inheritdoc cref="UseElasticDefaults(MeterProviderBuilder)" /></returns>
	public static MeterProviderBuilder UseElasticDefaults(this MeterProviderBuilder builder, ElasticOpenTelemetryOptions options)
	{
#if NET
		ArgumentNullException.ThrowIfNull(options);
#else
		if (options is null)
			throw new ArgumentNullException(nameof(options));
#endif

		return UseElasticDefaultsCore(builder, new(options), null);
	}

	/// <summary>
	/// <inheritdoc cref="UseElasticDefaults(MeterProviderBuilder)" />
	/// </summary>
	/// <param name="builder"><inheritdoc cref="UseElasticDefaults(MeterProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configuration">An <see cref="IConfiguration"/> instance from which to load the Elastic Distribution of
	/// OpenTelemetry (EDOT) options.</param>
	/// <returns><inheritdoc cref="UseElasticDefaults(MeterProviderBuilder)" /></returns>
	public static MeterProviderBuilder UseElasticDefaults(this MeterProviderBuilder builder, IConfiguration configuration)
	{
#if NET
		ArgumentNullException.ThrowIfNull(configuration);
#else
		if (configuration is null)
			throw new ArgumentNullException(nameof(configuration));
#endif
		return UseElasticDefaultsCore(builder, new(configuration), null);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static MeterProviderBuilder UseElasticDefaults(
		this MeterProviderBuilder builder,
		ElasticOpenTelemetryComponents components) =>
			UseElasticDefaultsCore(builder, components.Options, components, null);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static MeterProviderBuilder UseElasticDefaults(
		this MeterProviderBuilder builder,
		ElasticOpenTelemetryComponents components,
		IServiceCollection serviceCollection) =>
			UseElasticDefaultsCore(builder, components.Options, components, serviceCollection);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static MeterProviderBuilder UseElasticDefaults(
		this MeterProviderBuilder builder,
		IConfiguration configuration,
		IServiceCollection serviceCollection) =>
			UseElasticDefaultsCore(builder, new(configuration), null, serviceCollection);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static MeterProviderBuilder UseElasticDefaults(
		this MeterProviderBuilder builder,
		IServiceCollection serviceCollection) =>
			UseElasticDefaultsCore(builder, null, null, serviceCollection);

	[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "The calls to `AddSqlClientInstrumentation` and `AssemblyScanning.AddInstrumentationViaReflection` " +
			"are guarded by a RuntimeFeature.IsDynamicCodeSupported` check and therefore this method is safe to call in AoT scenarios.")]
	internal static MeterProviderBuilder UseElasticDefaultsCore(
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
			if (!SignalBuilder.ConfigureBuilder(nameof(UseElasticDefaults), providerBuilderName, builder,
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
			try
			{
				// This first check determines whether OpenTelemetry.Instrumentation.Http.dll is present, in which case,
				// it will be registered on the builder via reflection. If it's not present, we can safely add the native
				// source which is OTel compliant since .NET 9.
				var assemblyLocation = Path.GetDirectoryName(AppContext.BaseDirectory);
				if (assemblyLocation is not null)
				{
					var assemblyPath = Path.Combine(assemblyLocation, "OpenTelemetry.Instrumentation.Http.dll");

					if (!File.Exists(assemblyPath))
					{
						AddWithLogging(builder, components.Logger, "Http (via native instrumentation)", b => b.AddMeter("System.Net.Http"));
					}
					else
					{
						components.Logger.LogHttpInstrumentationFound(assemblyPath, "metric");
					}
				}
			}
			catch (Exception ex)
			{
				components.Logger.LogError(ex, "An exception occurred while checking for the presence of `OpenTelemetry.Instrumentation.Http.dll`.");
			}
#else
			AddWithLogging(builder, components.Logger, "Http (via contrib instrumentation)", b => b.AddHttpClientInstrumentation());
#endif

			AddWithLogging(builder, components.Logger, "Process", b => b.AddProcessInstrumentation());
#if NET9_0_OR_GREATER
			AddWithLogging(builder, components.Logger, "Runtime", b => b.AddMeter("System.Runtime"));
#else
			AddWithLogging(builder, components.Logger, "Runtime", b => b.AddRuntimeInstrumentation());
#endif

#if NET8_0_OR_GREATER
			if (RuntimeFeature.IsDynamicCodeSupported)
#endif
			{
				SignalBuilder.AddInstrumentationViaReflection(builder, components.Logger, GetReflectionInstrumentationAssemblies());
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
