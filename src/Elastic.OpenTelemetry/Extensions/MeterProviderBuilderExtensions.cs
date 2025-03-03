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
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

// Matching namespace with MeterProviderBuilder
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace OpenTelemetry.Metrics;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides extension methods on the <see cref="MeterProviderBuilder"/> used to register
/// the Elastic Distribution of OpenTelemetry (EDOT) .NET defaults.
/// </summary>
public static class MeterProviderBuilderExtensions
{
	/// <summary>
	/// Used to track the number of times any variation of `WithElasticDefaults` is invoked by consuming
	/// code acrosss all <see cref="MeterProviderBuilder"/> instances. This allows us to warn about potenital
	/// misconfigurations.
	/// </summary>
	private static int WithElasticDefaultsCallCount;

	/// <summary>
	/// Use Elastic Distribution of OpenTelemetry .NET defaults for <see cref="MeterProviderBuilder"/>.
	/// </summary>
	/// <remarks>
	/// Calling this method is not neccesary if <see cref="OpenTelemetryBuilderExtensions.WithElasticDefaults(IOpenTelemetryBuilder)"/>
	/// has been called previously as that automatically adds the defaults for all signals.
	/// </remarks>
	/// <param name="builder">The <see cref="MeterProviderBuilder"/> to configure.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <returns>The <see cref="MeterProviderBuilder"/> for chaining configuration.</returns>
	public static MeterProviderBuilder WithElasticDefaults(this MeterProviderBuilder builder)
	{
#if NET
		ArgumentNullException.ThrowIfNull(builder);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));
#endif

		return WithElasticDefaultsCore(builder, null, null, null);
	}

	/// <summary>
	/// <inheritdoc cref="WithElasticDefaults(MeterProviderBuilder)" />
	/// </summary>
	/// <remarks><inheritdoc cref="WithElasticDefaults(MeterProviderBuilder)" /></remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(MeterProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="skipOtlpExporter">When registering Elastic defaults, skip automatic registration of the OTLP exporter for metrics.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <returns><inheritdoc cref="WithElasticDefaults(MeterProviderBuilder)" /></returns>
	public static MeterProviderBuilder WithElasticDefaults(this MeterProviderBuilder builder, bool skipOtlpExporter)
	{
#if NET
		ArgumentNullException.ThrowIfNull(builder);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));
#endif

		return WithElasticDefaultsCore(builder, skipOtlpExporter ? CompositeElasticOpenTelemetryOptions.SkipOtlpOptions : null, null, null);
	}

	/// <summary>
	/// <inheritdoc cref="WithElasticDefaults(MeterProviderBuilder)" />
	/// </summary>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(MeterProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="options"><see cref="ElasticOpenTelemetryOptions"/> used to configure the Elastic Distribution of OpenTelemetry (EDOT) .NET.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="options"/> is null.</exception>
	/// <returns><inheritdoc cref="WithElasticDefaults(MeterProviderBuilder)" /></returns>
	public static MeterProviderBuilder WithElasticDefaults(this MeterProviderBuilder builder, ElasticOpenTelemetryOptions options)
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

		return WithElasticDefaultsCore(builder, new(options), null, null);
	}

	/// <summary>
	/// <inheritdoc cref="WithElasticDefaults(MeterProviderBuilder)" />
	/// </summary>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(MeterProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configuration">An <see cref="IConfiguration"/> instance from which to load the Elastic Distribution of
	/// OpenTelemetry (EDOT) .NET options.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configuration"/> is null.</exception>
	/// <returns><inheritdoc cref="WithElasticDefaults(MeterProviderBuilder)" /></returns>
	public static MeterProviderBuilder WithElasticDefaults(this MeterProviderBuilder builder, IConfiguration configuration)
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
		return WithElasticDefaultsCore(builder, new(configuration), null, null);
	}

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
		IServiceCollection? services)
	{
		var logger = SignalBuilder.GetLogger(builder, components, options, null);

		var callCount = Interlocked.Increment(ref WithElasticDefaultsCallCount);

		if (callCount > 1)
		{
			logger.LogMultipleWithElasticDefaultsCallsWarning(callCount, nameof(MeterProviderBuilder));
		}
		else
		{
			logger.LogWithElasticDefaultsCallCount(callCount, nameof(MeterProviderBuilder));
		}

		return SignalBuilder.WithElasticDefaults(builder, Signals.Traces, options, components, services, ConfigureBuilder);

		static void ConfigureBuilder(MeterProviderBuilder builder, BuilderState builderState, IServiceCollection? services)
		{
			const string loggingProviderName = nameof(MeterProviderBuilder);
			var components = builderState.Components;
			var logger = components.Logger;

			logger.LogConfiguringBuilder(loggingProviderName, builderState.InstanceIdentifier);

			builder.ConfigureResource(r => r.WithElasticDefaults(builderState, services));

#if NET9_0_OR_GREATER
			// On .NET 9, the contrib HTTP instrumentation is no longer required. If the dependency exists,
			// it will be registered via the reflection-based assembly scanning.
			if (SignalBuilder.InstrumentationAssemblyExists("OpenTelemetry.Instrumentation.Http.dll"))
			{
				logger.LogHttpInstrumentationFound("metric", nameof(MeterProviderBuilder), builderState.InstanceIdentifier);

				// For native AOT scenarios, the reflection-based assembly scanning will not run.
				// Therefore, we log a warning since no HTTP instrumentation will be automatically registered.
				// In this scenario, the consumer must register the contrib instrumentation manually, or
				// remove the dependency so that the native .NET 9 HTTP instrumentation source will be added
				// instead.
				if (!RuntimeFeature.IsDynamicCodeSupported)
					logger.LogWarning("The OpenTelemetry.Instrumentation.Http.dll was found alongside the executing assembly. " +
						"When using Native AOT publishing on .NET, the metric instrumentation is not registered automatically. Either register it manually, " +
						"or remove the dependency so that the native `System.Net.Http` instrumentation (available in .NET 9) is observed instead.");
			}
			else
			{
				AddMeterWithLogging(builder, logger, "System.Net.Http", builderState.InstanceIdentifier);
			}

			// On .NET 9, the contrib runtime instrumentation is no longer required. If the dependency exists,
			// it will be registered via the reflection-based assembly scanning.
			if (SignalBuilder.InstrumentationAssemblyExists("OpenTelemetry.Instrumentation.Runtime.dll"))
			{
				logger.LogRuntimeInstrumentationFound();

				// For native AOT scenarios, the reflection-based assembly scanning will not run.
				// Therefore, we log a warning since no runtime metric instrumentation will be automatically registered.
				// In this scenario, the consumer must register the contrib instrumentation manually, or
				// remove the dependency so that the native .NET 9 runtime metric instrumentation source will be added
				// instead.
				if (!RuntimeFeature.IsDynamicCodeSupported)
					logger.LogWarning("The OpenTelemetry.Instrumentation.Runtime.dll was found alongside the executing assembly. " +
						"When using Native AOT publishing on .NET, the metric instrumentation is not registered automatically. Either register it manually, " +
						"or remove the dependency so that the native `System.Runtime` instrumentation (available in .NET 9) is observed instead.");
			}
			else
			{
				AddMeterWithLogging(builder, logger, "System.Runtime", builderState.InstanceIdentifier);
			}
#else
			AddWithLogging(builder, logger, "HTTP", b => b.AddHttpClientInstrumentation(), builderState.InstanceIdentifier);
			AddWithLogging(builder, logger, "Runtime", b => b.AddRuntimeInstrumentation(), builderState.InstanceIdentifier);
#endif
			// We explicity include this dependency and add it, since the current curated metric dashboard requires the memory metric.
			AddWithLogging(builder, logger, "Process", b => b.AddProcessInstrumentation(), builderState.InstanceIdentifier);

			if (SignalBuilder.InstrumentationAssemblyExists("OpenTelemetry.Instrumentation.AspNetCore.dll"))
			{
				AddMeterWithLogging(builder, logger, "Microsoft.AspNetCore.Hosting", builderState.InstanceIdentifier);
				AddMeterWithLogging(builder, logger, "Microsoft.AspNetCore.Routing", builderState.InstanceIdentifier);
				AddMeterWithLogging(builder, logger, "Microsoft.AspNetCore.Diagnostics", builderState.InstanceIdentifier);
				AddMeterWithLogging(builder, logger, "Microsoft.AspNetCore.RateLimiting", builderState.InstanceIdentifier);
				AddMeterWithLogging(builder, logger, "Microsoft.AspNetCore.HeaderParsing", builderState.InstanceIdentifier);
				AddMeterWithLogging(builder, logger, "Microsoft.AspNetCore.Server.Kestrel", builderState.InstanceIdentifier);
				AddMeterWithLogging(builder, logger, "Microsoft.AspNetCore.Http.Connections", builderState.InstanceIdentifier);
			}

			AddMeterWithLogging(builder, logger, "System.Net.NameResolution", builderState.InstanceIdentifier);

#if NET
			if (RuntimeFeature.IsDynamicCodeSupported)
#endif
			{
				SignalBuilder.AddInstrumentationViaReflection(builder, builderState.Components,
					ContribMetricsInstrumentation.GetMetricsInstrumentationAssembliesInfo(), builderState.InstanceIdentifier);
			}

			if (components.Options.SkipOtlpExporter)
			{
				logger.LogSkippingOtlpExporter(nameof(Signals.Traces), nameof(MeterProviderBuilder), builderState.InstanceIdentifier);
			}
			else
			{
				builder.AddOtlpExporter();
			}

			logger.LogConfiguredSignalProvider(nameof(Signals.Logs), nameof(MeterProviderBuilder), builderState.InstanceIdentifier);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static void AddMeterWithLogging(MeterProviderBuilder builder, ILogger logger, string meterName, string builderIdentifier)
		{
			builder.AddMeter(meterName);
			logger.LogMeterAdded(meterName, builderIdentifier);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static void AddWithLogging(MeterProviderBuilder builder, ILogger logger, string name, Action<MeterProviderBuilder> add, string builderIdentifier)
		{
			add.Invoke(builder);
			logger.LogAddedInstrumentation(name, nameof(MeterProviderBuilder), builderIdentifier);
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
			builder.ConfigureResource(r => r.WithElasticDefaults(components, null));

			if (components.Options.SkipOtlpExporter)
			{
				components.Logger.LogSkippingOtlpExporter(nameof(Signals.Metrics), nameof(MeterProviderBuilder), "<n/a>");
			}
			else
			{
				builder.AddOtlpExporter();
			}

			components.Logger.LogConfiguredSignalProvider(nameof(Signals.Metrics), nameof(MeterProviderBuilder), "<n/a>");

			return builder;
		}
		catch (Exception ex)
		{
			components.Logger.LogError(new EventId(521, "AutoInstrumentationTracerFailure"), ex,
				"Failed to register EDOT defaults for metrics auto-instrumentation to the MeterProviderBuilder.");
		}

		return builder;
	}
}
