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
	public static TracerProviderBuilder WithElasticDefaults(this TracerProviderBuilder builder)
	{
#if NET
        ArgumentNullException.ThrowIfNull(builder);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));
#endif

		return WithElasticDefaultsCore(builder, null, null);
	}

	/// <summary>
	/// <inheritdoc cref="WithElasticDefaults(TracerProviderBuilder)" />
	/// </summary>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(TracerProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="skipOtlpExporter">When registering Elastic defaults, skip automatic registration of the OTLP exporter for traces.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <returns><inheritdoc cref="WithElasticDefaults(TracerProviderBuilder)" /></returns>
	public static TracerProviderBuilder WithElasticDefaults(this TracerProviderBuilder builder, bool skipOtlpExporter)
	{
#if NET
        ArgumentNullException.ThrowIfNull(builder);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));
#endif

		return WithElasticDefaultsCore(builder, skipOtlpExporter ? CompositeElasticOpenTelemetryOptions.SkipOtlpOptions : null, null);
	}

	/// <summary>
	/// <inheritdoc cref="WithElasticDefaults(TracerProviderBuilder)" />
	/// </summary>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(TracerProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="options"><see cref="ElasticOpenTelemetryOptions"/> used to configure the Elastic Distribution of OpenTelemetry (EDOT) for .NET.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="options"/> is null.</exception>
	/// <returns><inheritdoc cref="WithElasticDefaults(TracerProviderBuilder)" /></returns>
	public static TracerProviderBuilder WithElasticDefaults(this TracerProviderBuilder builder, ElasticOpenTelemetryOptions options)
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

		return WithElasticDefaultsCore(builder, new(options), null);
	}

	/// <summary>
	/// <inheritdoc cref="WithElasticDefaults(TracerProviderBuilder)" />
	/// </summary>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(TracerProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configuration">An <see cref="IConfiguration"/> instance from which to load the OpenTelemetry SDK options.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configuration"/> is null.</exception>
	/// <returns><inheritdoc cref="WithElasticDefaults(TracerProviderBuilder)" /></returns>
	public static TracerProviderBuilder WithElasticDefaults(this TracerProviderBuilder builder, IConfiguration configuration)
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
		return WithElasticDefaultsCore(builder, new(configuration));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static TracerProviderBuilder WithElasticDefaults(this TracerProviderBuilder builder, ElasticOpenTelemetryComponents components) =>
		WithElasticDefaultsCore(builder, components.Options, components);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static TracerProviderBuilder WithElasticDefaults(
		this TracerProviderBuilder builder,
		IConfiguration configuration,
		IServiceCollection serviceCollection) =>
			WithElasticDefaultsCore(builder, new(configuration), null, serviceCollection);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static TracerProviderBuilder WithElasticDefaults(this TracerProviderBuilder builder, ElasticOpenTelemetryComponents components, IServiceCollection? services) =>
		WithElasticDefaultsCore(builder, components.Options, components, services);

	private static TracerProviderBuilder WithElasticDefaultsCore(
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
			if (!SignalBuilder.ConfigureBuilder(nameof(WithElasticDefaults), providerBuilderName, builder,
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
			if (SignalBuilder.InstrumentationAssemblyExists("OpenTelemetry.Instrumentation.Http.dll"))
			{
				components.Logger.LogHttpInstrumentationFound("trace");

				if (!RuntimeFeature.IsDynamicCodeSupported)
					components.Logger.LogWarning("The OpenTelemetry.Instrumentation.Http.dll was found alongside the executing assembly. " +
						"When using Native AOT publishing on .NET, the trace instrumentation is not registered automatically. Either register it manually, " +
						"or remove the dependency so that the native `System.Net.Http` instrumentation (available in .NET 9) is observed instead.");
			}
			else
			{
				AddWithLogging(builder, components.Logger, "HTTP (via native instrumentation)", b => b.AddSource("System.Net.Http"));
			}
#else
			AddWithLogging(builder, components.Logger, "HTTP (via contrib instrumentation)", b => b.AddHttpClientInstrumentation());
#endif

			AddWithLogging(builder, components.Logger, "GrpcClient", b => b.AddGrpcClientInstrumentation());
			AddWithLogging(builder, components.Logger, "ElasticTransport", b => b.AddSource("Elastic.Transport"));

			// NOTE: Despite them having no dependencies. We cannot add the OpenTelemetry.Instrumentation.ElasticsearchClient or
			// OpenTelemetry.Instrumentation.EntityFrameworkCore instrumentations here, as including the package references causes
			// trimming warnings. We can still add them via reflection.

#if NET
			if (RuntimeFeature.IsDynamicCodeSupported)
#endif
			{
				// This instrumentation is not currently compatible for AoT scenarios.
				AddWithLogging(builder, components.Logger, "SqlClient", b => b.AddSqlClientInstrumentation());
				SignalBuilder.AddInstrumentationViaReflection(builder, components, ContribTraceInstrumentation.GetReflectionInstrumentationAssemblies());
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
	/// It is not neccessary to call this method if `WithElasticDefaults` has already been called.
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
			if (!SignalBuilder.ConfigureBuilder(nameof(WithElasticDefaults), nameof(TracerProviderBuilder), builder,
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
