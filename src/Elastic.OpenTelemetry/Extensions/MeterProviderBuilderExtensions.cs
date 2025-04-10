// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Elastic.OpenTelemetry;
using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Core;
using Elastic.OpenTelemetry.Diagnostics;
using Elastic.OpenTelemetry.Exporters;
using Elastic.OpenTelemetry.Instrumentation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

// Matching namespace with MeterProviderBuilder
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace OpenTelemetry;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides extension methods on the <see cref="MeterProviderBuilder"/> used to register
/// the Elastic Distribution of OpenTelemetry (EDOT) .NET defaults.
/// </summary>
public static class MeterProviderBuilderExtensions
{
	/// <summary>
	/// Used to track the number of times any variation of `WithElasticDefaults` is invoked by consuming
	/// code across all <see cref="MeterProviderBuilder"/> instances. This allows us to warn about potential
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
			const string meterProviderBuilderName = nameof(MeterProviderBuilder);
			var components = builderState.Components;
			var logger = components.Logger;

			logger.LogConfiguringBuilder(meterProviderBuilderName, builderState.InstanceIdentifier);

			builder.ConfigureResource(r => r.WithElasticDefaults(builderState, services));

			// When services is not null here, the options will have already been configured by the calling code.
			if (services is null)
				builder.ConfigureServices(sc => sc.Configure<OtlpExporterOptions>(OtlpExporterDefaults.OtlpExporterOptions));

			builder.ConfigureServices(sc => sc.Configure<MetricReaderOptions>(o =>
				o.TemporalityPreference = MetricReaderTemporalityPreference.Delta));

#if NET9_0_OR_GREATER
			// .NET 9 introduced semantic convention compatible instrumentation in System.Net.Http so it's recommended to no longer
			// use the contrib instrumentation. We don't bring in the dependency for .NET 9+. However, if the consuming app depends
			// on it, it will be assumed that the user prefers it and therefore we allow the assembly scanning to add it. We don't
			// add the native meter to avoid doubling up on metrics.
			if (SignalBuilder.InstrumentationAssemblyExists("OpenTelemetry.Instrumentation.Http.dll"))
			{
				logger.LogHttpInstrumentationFound("metric", meterProviderBuilderName, builderState.InstanceIdentifier);

				if (!RuntimeFeature.IsDynamicCodeSupported)
					logger.LogWarning("The OpenTelemetry.Instrumentation.Http.dll was found alongside the executing assembly. " +
						"When using Native AOT publishing on .NET, the metrics instrumentation is not registered automatically. Either register it manually, " +
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
#endif

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
	}
}
