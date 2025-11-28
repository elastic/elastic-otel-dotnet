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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

// Matching namespace with MeterProviderBuilder
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace OpenTelemetry;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides extension methods on the <see cref="MeterProviderBuilder"/> used to register
/// the Elastic Distribution of OpenTelemetry (EDOT) .NET <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/setup/edot-defaults">defaults</see>
/// for metrics.
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
	/// <para>
	///   This method is intended for advanced scenarios where per signal Elastic Distribution of OpenTelemetry (EDOT) .NET
	///   defaults are being enabled. In most scenarios, prefer the <c>AddElasticOpenTelemetry</c> method on <see cref="IHostApplicationBuilder"/>
	///   or <see cref="IServiceCollection"/>.
	/// </para>
	/// <para>
	///   <b>Warning:</b> Avoid calling this method when using the <c>AddElasticOpenTelemetry</c> method on <see cref="IHostApplicationBuilder"/>
	///   or <see cref="IServiceCollection"/>. In those scenarios, the OpenTelemetry SDK will have already been configured with
	///   defaults, including the OTLP exporter. Calling this method again may lead to unexpected behavior.
	/// </para>
	/// <para>
	///   When using <c>AddElasticOpenTelemetry</c>, use the overload on the <see cref="IHostApplicationBuilder"/>
	///   or <see cref="IServiceCollection"/> which accepts a configuration <see cref="Action{IOpenTelemetryBuilder}"/>
	///   to further customize the SDK setup. This action is invoked after EDOT .NET defaults have been applied, but
	///   before the OTLP exporter is added.
	/// </para>
	/// <para>
	///   <b>Note:</b> If you wish to customise the <see cref="MeterProviderBuilder"/> after the EDOT .NET defaults have been applied,
	///   use the <see cref="WithElasticDefaults(MeterProviderBuilder, Action{MeterProviderBuilder})"/> overload that accepts a
	///   configuration action.
	/// </para>
	/// </remarks>
	/// <param name="builder">The <see cref="MeterProviderBuilder"/> to configure.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <returns>
	/// An instance of <see cref="MeterProviderBuilder"/> that can be used to further configure the metrics signal.
	/// <para>
	///   <b>Warning:</b> Any further configuration applied via the returned <see cref="MeterProviderBuilder"/> will
	///   be applied <em>after</em> the OTLP exporter has been added.
	/// </para>
	/// <para>
	///   <b>Recommendation:</b>It is strongly recommended to use the <see cref="WithElasticDefaults(MeterProviderBuilder, Action{MeterProviderBuilder})"/>
	///   overload instead. This accepts an <see cref="Action{MeterProviderBuilder}"/> configuration callback and ensures any
	///   customisations are applied before the OTLP exporter is added.
	/// </para>
	/// <para>
	///   Alternatively, the <see cref="ElasticOpenTelemetryOptions.SkipOtlpExporter"/>
	///   <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/configuration#skipotlpexporter">option</see>
	///   automatic addition of the OTLP exporter, allowing you to add it manually at a later stage.
	/// </para>
	/// </returns>
	public static MeterProviderBuilder WithElasticDefaults(this MeterProviderBuilder builder)
	{
#if NET
		ArgumentNullException.ThrowIfNull(builder);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));
#endif

		return WithElasticDefaultsCore(builder, null, null, null, default);
	}

	/// <summary><inheritdoc cref="WithElasticDefaults(MeterProviderBuilder)" /></summary>
	/// <remarks>
	/// <para>
	///   This method is intended for advanced scenarios where per signal Elastic Distribution of OpenTelemetry (EDOT) .NET
	///   defaults are being enabled. In most scenarios, prefer the <c>AddElasticOpenTelemetry</c> method on <see cref="IHostApplicationBuilder"/>
	///   or <see cref="IServiceCollection"/>.
	/// </para>
	/// <para>
	///   <b>Warning:</b> Avoid calling this method when using the <c>AddElasticOpenTelemetry</c> method on <see cref="IHostApplicationBuilder"/>
	///   or <see cref="IServiceCollection"/>. In those scenarios, the OpenTelemetry SDK will have already been configured with
	///   defaults, including the OTLP exporter. Calling this method again may lead to unexpected behavior.
	/// </para>
	/// <para>
	///   When using <c>AddElasticOpenTelemetry</c>, use the overload on the <see cref="IHostApplicationBuilder"/>
	///   or <see cref="IServiceCollection"/> which accepts a configuration <see cref="Action{IOpenTelemetryBuilder}"/>
	///   to further customize the SDK setup. This action is invoked after EDOT .NET defaults have been applied, but
	///   before the OTLP exporter is added.
	/// </para>
	/// </remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(MeterProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configureBuilder">
	/// An <see cref="Action"/> used to further configure the <see cref="MeterProviderBuilder"/>.
	/// This action is invoked after <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/setup/edot-defaults">EDOT .NET defaults</see>
	/// have been applied, but before the OTLP exporter is added. This ensures that any custom processors run before the exporter.
	/// </param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configureBuilder"/> is null.</exception>
	/// <returns>
	/// <para>
	///   An instance of <see cref="MeterProviderBuilder"/> that can be used to further configure the metrics signal.
	/// </para>
	/// <para>
	///   <b>Warning:</b> Any further configuration applied via the returned <see cref="MeterProviderBuilder"/> will
	///   be applied <em>after</em> the OTLP exporter has been added.
	/// </para>
	/// <para>
	///   <b>Recommendation:</b>It is strongly recommended to use the <paramref name="configureBuilder"/> action to customise
	///   the <see cref="MeterProviderBuilder"/> after EDOT .NET defaults are applied and before the OTLP exporter is added.
	/// </para>
	/// </returns>
	public static MeterProviderBuilder WithElasticDefaults(this MeterProviderBuilder builder, Action<MeterProviderBuilder> configureBuilder)
	{
#if NET
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configureBuilder);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (configureBuilder is null)
			throw new ArgumentNullException(nameof(configureBuilder));
#endif

		var builderOptions = new BuilderOptions<MeterProviderBuilder> { UserProvidedConfigureBuilder = configureBuilder };
		return WithElasticDefaultsCore(builder, null, null, null, builderOptions);
	}

	/// <summary><inheritdoc cref="WithElasticDefaults(MeterProviderBuilder)" /></summary>
	/// <remarks>
	/// <para>
	///   This method is intended for advanced scenarios where per signal Elastic Distribution of OpenTelemetry (EDOT) .NET
	///   defaults are being enabled. In most scenarios, prefer the <c>AddElasticOpenTelemetry</c> method on <see cref="IHostApplicationBuilder"/>
	///   or <see cref="IServiceCollection"/>.
	/// </para>
	/// <para>
	///   <b>Warning:</b> Avoid calling this method when using the <c>AddElasticOpenTelemetry</c> method on <see cref="IHostApplicationBuilder"/>
	///   or <see cref="IServiceCollection"/>. In those scenarios, the OpenTelemetry SDK will have already been configured with
	///   defaults, including the OTLP exporter. Calling this method again may lead to unexpected behavior.
	/// </para>
	/// <para>
	///   When using <c>AddElasticOpenTelemetry</c>, use the overload on the <see cref="IHostApplicationBuilder"/>
	///   or <see cref="IServiceCollection"/> which accepts a configuration <see cref="Action{IOpenTelemetryBuilder}"/>
	///   to further customize the SDK setup. This action is invoked after EDOT .NET defaults have been applied, but
	///   before the OTLP exporter is added.
	/// </para>
	/// <para>
	///   <b>Note:</b> If you wish to customise the <see cref="MeterProviderBuilder"/> after the EDOT .NET defaults have been applied,
	///   use the <see cref="WithElasticDefaults(MeterProviderBuilder, ElasticOpenTelemetryOptions, Action{MeterProviderBuilder})"/> overload that accepts a
	///   configuration action.
	/// </para>
	/// </remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(MeterProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="options"><see cref="ElasticOpenTelemetryOptions"/> used to configure the Elastic Distribution of OpenTelemetry (EDOT) .NET.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="options"/> is null.</exception>
	/// <returns>
	/// An instance of <see cref="MeterProviderBuilder"/> that can be used to further configure the metrics signal.
	/// <para>
	///   <b>Warning:</b> Any further configuration applied via the returned <see cref="MeterProviderBuilder"/> will
	///   be applied <em>after</em> the OTLP exporter has been added.
	/// </para>
	/// <para>
	///   <b>Recommendation:</b>It is strongly recommended to use the <see cref="WithElasticDefaults(MeterProviderBuilder, ElasticOpenTelemetryOptions, Action{MeterProviderBuilder})"/>
	///   overload instead. This accepts an <see cref="Action{MeterProviderBuilder}"/> configuration callback and ensures any
	///   customisations are applied before the OTLP exporter is added.
	/// </para>
	/// <para>
	///   Alternatively, the <see cref="ElasticOpenTelemetryOptions.SkipOtlpExporter"/>
	///   <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/configuration#skipotlpexporter">option</see>
	///   automatic addition of the OTLP exporter, allowing you to add it manually at a later stage.
	/// </para>
	/// </returns>
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

		return WithElasticDefaultsCore(builder, new(options), null, null, default);
	}

	/// <summary><inheritdoc cref="WithElasticDefaults(MeterProviderBuilder)" /></summary>
	/// <remarks><inheritdoc cref="WithElasticDefaults(MeterProviderBuilder, Action{MeterProviderBuilder})" /></remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(MeterProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="options"><inheritdoc cref="WithElasticDefaults(MeterProviderBuilder, ElasticOpenTelemetryOptions)" path="/param[@name='options']"/></param>
	/// <param name="configureBuilder"><inheritdoc cref="WithElasticDefaults(MeterProviderBuilder, Action{MeterProviderBuilder})" path="/param[@name='configureBuilder']"/></param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="options"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configureBuilder"/> is null.</exception>
	/// <returns><inheritdoc cref="WithElasticDefaults(MeterProviderBuilder, Action{MeterProviderBuilder})" /></returns>
	public static MeterProviderBuilder WithElasticDefaults(this MeterProviderBuilder builder,
		ElasticOpenTelemetryOptions options, Action<MeterProviderBuilder> configureBuilder)
	{
#if NET
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(configureBuilder);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (options is null)
			throw new ArgumentNullException(nameof(options));

		if (configureBuilder is null)
			throw new ArgumentNullException(nameof(configureBuilder));
#endif
		var builderOptions = new BuilderOptions<MeterProviderBuilder> { UserProvidedConfigureBuilder = configureBuilder };
		return WithElasticDefaultsCore(builder, new(options), null, null, builderOptions);
	}

	/// <summary><inheritdoc cref="WithElasticDefaults(MeterProviderBuilder)" /></summary>
	/// <remarks>
	/// <para>
	///   This method is intended for advanced scenarios where per signal Elastic Distribution of OpenTelemetry (EDOT) .NET
	///   defaults are being enabled. In most scenarios, prefer the <c>AddElasticOpenTelemetry</c> method on <see cref="IHostApplicationBuilder"/>
	///   or <see cref="IServiceCollection"/>.
	/// </para>
	/// <para>
	///   <b>Warning:</b> Avoid calling this method when using the <c>AddElasticOpenTelemetry</c> method on <see cref="IHostApplicationBuilder"/>
	///   or <see cref="IServiceCollection"/>. In those scenarios, the OpenTelemetry SDK will have already been configured with
	///   defaults, including the OTLP exporter. Calling this method again may lead to unexpected behavior.
	/// </para>
	/// <para>
	///   When using <c>AddElasticOpenTelemetry</c>, use the overload on the <see cref="IHostApplicationBuilder"/>
	///   or <see cref="IServiceCollection"/> which accepts a configuration <see cref="Action{IOpenTelemetryBuilder}"/>
	///   to further customize the SDK setup. This action is invoked after EDOT .NET defaults have been applied, but
	///   before the OTLP exporter is added.
	/// </para>
	/// <para>
	///   <b>Note:</b> If you wish to customise the <see cref="MeterProviderBuilder"/> after the EDOT .NET defaults have been applied,
	///   use the <see cref="WithElasticDefaults(MeterProviderBuilder, IConfiguration, Action{MeterProviderBuilder})"/> overload that accepts a
	///   configuration action.
	/// </para>
	/// </remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(MeterProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configuration">An <see cref="IConfiguration"/> instance from which to load the Elastic Distribution of
	/// OpenTelemetry (EDOT) .NET options.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configuration"/> is null.</exception>
	/// <returns>
	/// An instance of <see cref="MeterProviderBuilder"/> that can be used to further configure the metrics signal.
	/// <para>
	///   <b>Warning:</b> Any further configuration applied via the returned <see cref="MeterProviderBuilder"/> will
	///   be applied <em>after</em> the OTLP exporter has been added.
	/// </para>
	/// <para>
	///   <b>Recommendation:</b>It is strongly recommended to use the <see cref="WithElasticDefaults(MeterProviderBuilder, IConfiguration, Action{MeterProviderBuilder})"/>
	///   overload instead. This accepts an <see cref="Action{MeterProviderBuilder}"/> configuration callback and ensures any
	///   customisations are applied before the OTLP exporter is added.
	/// </para>
	/// <para>
	///   Alternatively, the <see cref="ElasticOpenTelemetryOptions.SkipOtlpExporter"/>
	///   <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/configuration#skipotlpexporter">option</see>
	///   automatic addition of the OTLP exporter, allowing you to add it manually at a later stage.
	/// </para>
	/// </returns>
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
		return WithElasticDefaultsCore(builder, new(configuration), null, null, default);
	}

	/// <summary><inheritdoc cref="WithElasticDefaults(MeterProviderBuilder)" /></summary>
	/// <remarks><inheritdoc cref="WithElasticDefaults(MeterProviderBuilder, Action{MeterProviderBuilder})" /></remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(MeterProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configuration"><inheritdoc cref="WithElasticDefaults(MeterProviderBuilder, IConfiguration)" path="/param[@name='configuration']"/></param>
	/// <param name="configureBuilder"><inheritdoc cref="WithElasticDefaults(MeterProviderBuilder, Action{MeterProviderBuilder})" path="/param[@name='configureBuilder']"/></param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configuration"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configureBuilder"/> is null.</exception>
	/// <returns><inheritdoc cref="WithElasticDefaults(MeterProviderBuilder, Action{MeterProviderBuilder})" /></returns>
	public static MeterProviderBuilder WithElasticDefaults(this MeterProviderBuilder builder,
		IConfiguration configuration, Action<MeterProviderBuilder> configureBuilder)
	{
#if NET
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentNullException.ThrowIfNull(configureBuilder);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (configuration is null)
			throw new ArgumentNullException(nameof(configuration));

		if (configureBuilder is null)
			throw new ArgumentNullException(nameof(configureBuilder));
#endif
		var builderOptions = new BuilderOptions<MeterProviderBuilder> { UserProvidedConfigureBuilder = configureBuilder };
		return WithElasticDefaultsCore(builder, new(configuration), null, null, builderOptions);
	}

	[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "The calls to `AddSqlClientInstrumentation` and `AssemblyScanning.AddInstrumentationViaReflection` " +
		"are guarded by a RuntimeFeature.IsDynamicCodeSupported` check and therefore this method is safe to call in AoT scenarios.")]
	internal static MeterProviderBuilder WithElasticDefaultsCore(
		this MeterProviderBuilder builder,
		CompositeElasticOpenTelemetryOptions? options,
		ElasticOpenTelemetryComponents? components,
		IServiceCollection? services,
		in BuilderOptions<MeterProviderBuilder> builderOptions)
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

		return SignalBuilder.WithElasticDefaults(builder, options, components, services, builderOptions, ConfigureBuilder);

		static void ConfigureBuilder(BuilderContext<MeterProviderBuilder> builderContext)
		{
			var builder = builderContext.Builder;
			var builderState = builderContext.BuilderState;
			var components = builderState.Components;
			var logger = components.Logger;
			var services = builderContext.Services;

			// FullName may return null so we fallback to Name when required.
			var meterProviderBuilderName = builder.GetType().FullName ?? builder.GetType().Name;

			logger.LogConfiguringBuilder(meterProviderBuilderName, builderState.InstanceIdentifier);

			builder.ConfigureResource(r => r.WithElasticDefaults(builderState, services));

			// When services is not null here, the options will have already been configured by the calling code so
			// we don't need to do it again.
			if (services is null)
			{
				builder.ConfigureServices(sc => sc.Configure<OtlpExporterOptions>(OtlpExporterDefaults.OtlpExporterOptions));
				logger.LogConfiguredOtlpExporterOptions("metrics");
			}

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

			// Execute user-provided configuration action
			var userProvidedConfigureBuilder = builderContext.BuilderOptions.UserProvidedConfigureBuilder;
			if (userProvidedConfigureBuilder is not null)
			{
				userProvidedConfigureBuilder(builder);
				logger.LogInvokedConfigureAction(meterProviderBuilderName, builderState.InstanceIdentifier);
			}

			if (builderContext.BuilderOptions.DeferAddOtlpExporter)
			{
				logger.LogDeferredOtlpExporter(meterProviderBuilderName, builderState.InstanceIdentifier);
			}
			else
			{
				if (components.Options.SkipOtlpExporter)
				{
					logger.LogSkippedOtlpExporter(nameof(Signals.Metrics), meterProviderBuilderName, builderState.InstanceIdentifier);
				}
				else
				{
					builder.AddOtlpExporter();
					logger.LogAddedOtlpExporter(nameof(Signals.Metrics), meterProviderBuilderName, builderState.InstanceIdentifier);
				}
			}

			logger.LogConfiguredSignalProvider(nameof(Signals.Metrics), meterProviderBuilderName, builderState.InstanceIdentifier);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static void AddMeterWithLogging(MeterProviderBuilder builder, ILogger logger, string meterName, string builderIdentifier)
		{
			builder.AddMeter(meterName);
			logger.LogMeterAdded(meterName, builderIdentifier);
		}
	}
}
