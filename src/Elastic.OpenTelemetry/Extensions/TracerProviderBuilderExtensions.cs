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
using Elastic.OpenTelemetry.Processors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;

// Matching namespace with MeterProviderBuilder
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace OpenTelemetry.Trace;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods for <see cref="TracerProviderBuilder"/> used to register
/// the Elastic Distribution of OpenTelemetry (EDOT) defaults.
/// </summary>
public static class TracerProviderBuilderExtensions
{
	private static readonly GlobalProviderBuilderState GlobalTracerProviderBuilderState = new();

	// Notes:

	// - This is defined as a static method and allocates the array each time.
	//   This is intentional, as we expect this to be invoked once (or worst case, few times).
	//   After initialisation, the array is no longer required and can be reclaimed by the GC.
	//   This is likley to be overall more efficient for the common scenario as we don't keep
	//   an object alive for the lifetime of the application.
	
	// - It is IMPORTANT that we evaluate each instrumentation we add via reflection to ensure
	//   that duplicate registration is non-breaking. For example, calls to `AddAspNetCoreInstrumentation`
	//   are not idempotent and will result in multiple listeners being registered which also
	//   causes filters, etc. to run multiple times. For the built in components, this adds
	//   overhead, but doesn't duplicate spans, as the instrumentation enriches, but does not
	//   create activities. However, for any other instrumentation, we'd need to check on the
	//   implementation to ensure we don't duplicate spans if we call the AddXyz method in
	//   addition to the consumer. We will also document the libraries we scan for and recommend
	//   that consumers do not manually add the instrumentation that we will add automatically.
	//   Further enhancements may include an analyser or switching to source generators to
	//   conditionally add instrumentation libraries.
	private static InstrumentationAssemblyInfo[] GetReflectionInstrumentationAssemblies() =>
	[
		new()
		{
			Name = "AspNetCore",
			Filename = "OpenTelemetry.Instrumentation.AspNetCore.dll",
			FullyQualifiedType = "OpenTelemetry.Trace.AspNetCoreInstrumentationTracerProviderBuilderExtensions",
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
			Name = "HTTP",
			Filename = "OpenTelemetry.Instrumentation.Http.dll",
			FullyQualifiedType = "OpenTelemetry.Trace.HttpClientInstrumentationTracerProviderBuilderExtensions",
			InstrumentationMethod = "AddHttpClientInstrumentation"
		},
#endif

		new()
		{
			Name = "NEST",
			Filename = "OpenTelemetry.Instrumentation.ElasticsearchClient.dll",
			FullyQualifiedType = "OpenTelemetry.Trace.TracerProviderBuilderExtensions",
			InstrumentationMethod = "AddElasticsearchClientInstrumentation"
		},
	];

	/// <summary>
	/// Use Elastic Distribution of OpenTelemetry .NET defaults for <see cref="TracerProviderBuilder"/>.
	/// </summary>
	/// <remarks>
	/// This is not neccesary if <see cref="OpenTelemetryBuilderExtensions.WithElasticDefaults(IOpenTelemetryBuilder)"/>
	/// has been called previously as that automatically adds the .
	/// </remarks>
	/// <param name="builder">The <see cref="TracerProviderBuilder"/> to configure.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <returns>The <see cref="TracerProviderBuilder"/> for chaining configuration.</returns>
	public static TracerProviderBuilder UseElasticDefaults(this TracerProviderBuilder builder)
	{
#if NET
        ArgumentNullException.ThrowIfNull(builder);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));
#endif

		return UseElasticDefaultsCore(builder, null, null);
	}

	/// <summary>
	/// <inheritdoc cref="UseElasticDefaults(TracerProviderBuilder)" />
	/// </summary>
	/// <param name="builder"><inheritdoc cref="UseElasticDefaults(TracerProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="skipOtlpExporter">When registering Elastic defaults, skip automatic registration of the OTLP exporter for traces.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <returns><inheritdoc cref="UseElasticDefaults(TracerProviderBuilder)" /></returns>
	public static TracerProviderBuilder UseElasticDefaults(this TracerProviderBuilder builder, bool skipOtlpExporter)
	{
#if NET
        ArgumentNullException.ThrowIfNull(builder);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));
#endif

		return UseElasticDefaultsCore(builder, skipOtlpExporter ? CompositeElasticOpenTelemetryOptions.SkipOtlpOptions : null, null);
	}

	/// <summary>
	/// <inheritdoc cref="UseElasticDefaults(TracerProviderBuilder)" />
	/// </summary>
	/// <param name="builder"><inheritdoc cref="UseElasticDefaults(TracerProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="options"><see cref="ElasticOpenTelemetryOptions"/> used to configure the Elastic Distribution of OpenTelemetry (EDOT) for .NET.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="options"/> is null.</exception>
	/// <returns><inheritdoc cref="UseElasticDefaults(TracerProviderBuilder)" /></returns>
	public static TracerProviderBuilder UseElasticDefaults(this TracerProviderBuilder builder, ElasticOpenTelemetryOptions options)
	{
#if NET
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(options);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (options is null)
			throw new ArgumentNullException(nameof(options));
#endif

		return UseElasticDefaultsCore(builder, new(options), null);
	}

	/// <summary>
	/// <inheritdoc cref="UseElasticDefaults(TracerProviderBuilder)" />
	/// </summary>
	/// <param name="builder"><inheritdoc cref="UseElasticDefaults(TracerProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configuration">An <see cref="IConfiguration"/> instance from which to load the OpenTelemetry SDK options.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configuration"/> is null.</exception>
	/// <returns><inheritdoc cref="UseElasticDefaults(TracerProviderBuilder)" /></returns>
	public static TracerProviderBuilder UseElasticDefaults(this TracerProviderBuilder builder, IConfiguration configuration)
	{
#if NET
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (configuration is null)
			throw new ArgumentNullException(nameof(configuration));
#endif
		return UseElasticDefaultsCore(builder, new(configuration));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static TracerProviderBuilder UseElasticDefaults(this TracerProviderBuilder builder, ElasticOpenTelemetryComponents components) =>
		UseElasticDefaultsCore(builder, components.Options, components);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static TracerProviderBuilder UseElasticDefaults(
		this TracerProviderBuilder builder,
		IConfiguration configuration,
		IServiceCollection serviceCollection) =>
			UseElasticDefaultsCore(builder, new(configuration), null, serviceCollection);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static TracerProviderBuilder UseElasticDefaults(this TracerProviderBuilder builder, ElasticOpenTelemetryComponents components, IServiceCollection? services) =>
		UseElasticDefaultsCore(builder, components.Options, components, services);

	private static TracerProviderBuilder UseElasticDefaultsCore(
		TracerProviderBuilder builder,
		CompositeElasticOpenTelemetryOptions? options,
		ElasticOpenTelemetryComponents? components = null,
		IServiceCollection? services = null)
	{
		const string providerBuilderName = nameof(MeterProviderBuilder);

		var logger = SignalBuilder.GetLogger(components, options);

		// If the signal is disabled via configuration we skip any potential bootstrapping.
		if (!SignalBuilder.IsSignalEnabled(components, options, Signals.Traces, providerBuilderName, logger))
			return builder;

		try
		{
			if (!SignalBuilder.ConfigureBuilder(nameof(UseElasticDefaults), providerBuilderName, builder,
				GlobalTracerProviderBuilderState, options, services, ConfigureBuilder, ref components))
			{
				logger = SignalBuilder.GetLogger(components, options); // Update the logger we should use from the ref-returned components.
				logger.UnableToConfigureLoggingDefaultsError(providerBuilderName);
				return builder;
			}
		}
		catch (Exception ex)
		{
			logger?.LogError(ex, "Failed to fully register EDOT .NET tracer defaults for {ProviderBuilderType}.", providerBuilderName);
		}

		return builder;

		[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "The calls to `AddSqlClientInstrumentation` and `AssemblyScanning.AddInstrumentationViaReflection` " +
			"are guarded by a RuntimeFeature.IsDynamicCodeSupported` check and therefore this method is safe to call in AoT scenarios.")]
		static void ConfigureBuilder(TracerProviderBuilder builder, ElasticOpenTelemetryComponents components)
		{
			builder.ConfigureResource(r => r.AddElasticDistroAttributes());

#if NET9_0_OR_GREATER
			// This first check determines whether OpenTelemetry.Instrumentation.Http.dll is present, in which case,
			// it will be registered on the builder via reflection (except for AoT scenarios) or may be manually registered
			// by the consumer. If it's not present, we can safely add the native source which is OTel compliant since .NET 9.

			// Since .NET 9, the native activity source for HTTP instrumentation is OTel compliant and the recommended
			// way to observe HTTP instrumentation. The following code checks for conditions where we will add the native
			// source to the builder.
			var assemblyLocation = Path.GetDirectoryName(AppContext.BaseDirectory);
			if (!string.IsNullOrEmpty(assemblyLocation))
			{
				var assemblyPath = Path.Combine(assemblyLocation, "OpenTelemetry.Instrumentation.Http.dll");

				// When dynamic code is not supported (AoT scenarios) we add the .NET 9 native activity source for HTTP instrumentation
				// because it won't have already been loaded via our reflection-based assembly scanning. We also add that native source
				// when we cannot find the instrumentation assembly at all, in which case, it certainly is not being used.
				if (!RuntimeFeature.IsDynamicCodeSupported || !File.Exists(assemblyPath))
				{
					AddWithLogging(builder, components.Logger, "HTTP (via native instrumentation)", b => b.AddSource("System.Net.Http"));
				}
				else
				{
					components.Logger.LogHttpInstrumentationFound(assemblyPath, "trace");
				}
			}
#else
			AddWithLogging(builder, components.Logger, "HTTP (via contrib instrumentation)", b => b.AddHttpClientInstrumentation());
#endif

			AddWithLogging(builder, components.Logger, "GrpcClient", b => b.AddGrpcClientInstrumentation());
			AddWithLogging(builder, components.Logger, "ElasticTransport", b => b.AddSource("Elastic.Transport"));

			// NOTE: Despite them having no dependencies. We cannot add the OpenTelemetry.Instrumentation.ElasticsearchClient or
			// OpenTelemetry.Instrumentation.EntityFrameworkCore instrumentations here, as including the package references causes
			// trimming warnings. We can still add them via reflection.

#if NET8_0_OR_GREATER
			if (RuntimeFeature.IsDynamicCodeSupported)
#endif
			{
				// This instrumentation is not supported for AoT scenarios.
				AddWithLogging(builder, components.Logger, "SqlClient", b => b.AddSqlClientInstrumentation());

				SignalBuilder.AddInstrumentationViaReflection(builder, components.Logger, GetReflectionInstrumentationAssemblies());
			}

			AddElasticProcessorsCore(builder, components);

			if (components.Options.SkipOtlpExporter)
			{
				components.Logger.LogSkippingOtlpExporter(nameof(Signals.Traces), nameof(TracerProviderBuilder));
			}
			else
			{
				builder.AddOtlpExporter();
			}

			components.Logger.LogConfiguredSignalProvider(nameof(Signals.Traces), nameof(TracerProviderBuilder));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static void AddWithLogging(TracerProviderBuilder builder, ILogger logger, string name, Action<TracerProviderBuilder> add)
		{
			add.Invoke(builder);
			logger.LogAddedInstrumentation(name, nameof(TracerProviderBuilder));
		}
	}

	// We use a different method here to ensure we don't cause a crash depending on instrumentation libraries which are not present.
	// We can't assume that any DLLs are available besides OpenTelemetry.dll, which auto-instrumentation includes.
	// The auto instrumentation enables a set of default instrumentation of it's own, so we rely on that.
	// In the future, we can assess if we should copy instrumentation DLLs into the autoinstrumentation zip file and enable them.
	internal static TracerProviderBuilder UseAutoInstrumentationElasticDefaults(this TracerProviderBuilder builder, ElasticOpenTelemetryComponents components)
	{
		Debug.Assert(components is not null, "Components should not be null when invoked from the auto instrumentation.");

		try
		{
			builder
				.ConfigureResource(r => r.AddElasticDistroAttributes())
				.AddSource("Elastic.Transport")
				.AddElasticProcessorsCore(components);

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
			components?.Logger?.LogError(ex, "Failed to register EDOT defaults for tracing auto-instrumentation to the {Provider}.", nameof(TracerProviderBuilder));
		}

		return builder;
	}

	/// <summary>
	/// Include Elastic trace processors for best compatibility with Elastic Observability.
	/// </summary>
	/// <remarks>
	/// It is not neccessary to call this method if `UseElasticDefaults` has already been called.
	/// </remarks>
	/// <param name="builder">The <see cref="TracerProviderBuilder"/> where the Elastic trace
	/// processors should be added.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <returns>The <see cref="TracerProviderBuilder"/> for chaining.</returns>
	public static TracerProviderBuilder AddElasticProcessors(this TracerProviderBuilder builder)
	{
#if NET
		ArgumentNullException.ThrowIfNull(builder);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));
#endif

		return AddElasticProcessorsCore(builder, null);
	}

	private static TracerProviderBuilder AddElasticProcessorsCore(
		this TracerProviderBuilder builder,
		ElasticOpenTelemetryComponents? components)
	{
		var options = components?.Options ?? CompositeElasticOpenTelemetryOptions.DefaultOptions;

		try
		{
			if (!SignalBuilder.ConfigureBuilder(nameof(UseElasticDefaults), nameof(TracerProviderBuilder), builder,
				GlobalTracerProviderBuilderState, options, null, ConfigureBuilder, ref components))
			{
				var logger = components?.Logger ?? options?.AdditionalLogger;
				logger?.LogError("Unable to configure {Builder} with Elastic defaults.", nameof(TracerProviderBuilder));
				return builder;
			}
		}
		catch (Exception ex)
		{
			var exceptionLogger = components is not null ? components.Logger : options?.AdditionalLogger;
			exceptionLogger?.LogError(ex, "Failed to fully register EDOT .NET tracer defaults for {Provider}.", nameof(TracerProviderBuilder));
		}

		return builder;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static void ConfigureBuilder(TracerProviderBuilder builder, ElasticOpenTelemetryComponents components)
		{
			builder.LogAndAddProcessor(new ElasticCompatibilityProcessor(components.Logger), components.Logger);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static TracerProviderBuilder LogAndAddProcessor(this TracerProviderBuilder builder, BaseProcessor<Activity> processor, ILogger logger)
	{
		builder.AddProcessor(processor);
		logger.LogProcessorAdded(processor.GetType().ToString(), builder.GetType().Name);
		return builder;
	}
}
