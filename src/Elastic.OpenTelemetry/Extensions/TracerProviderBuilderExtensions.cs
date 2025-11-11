// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
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
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

// Matching namespace with TracerProviderBuilder
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace OpenTelemetry;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides extension methods on the <see cref="TracerProviderBuilder"/> used to register
/// the Elastic Distribution of OpenTelemetry (EDOT) .NET <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/setup/edot-defaults">defaults</see>.
/// </summary>
public static class TracerProviderBuilderExtensions
{
	private static int WithElasticDefaultsCallCount;

	/// <summary>
	/// Apply Elastic Distribution of OpenTelemetry (EDOT) .NET
	/// <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/setup/edot-defaults">defaults</see>
	/// to the <see cref="TracerProviderBuilder"/>.
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
	///   <b>Note:</b> If you wish to customise the <see cref="TracerProviderBuilder"/> after the EDOT .NET defaults have been applied,
	///   use the <see cref="WithElasticDefaults(TracerProviderBuilder, Action{TracerProviderBuilder})"/> overload that accepts a
	///   configuration action.
	/// </para>
	/// </remarks>
	/// <param name="builder">The <see cref="TracerProviderBuilder"/> to configure.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <returns>
	/// An instance of <see cref="TracerProviderBuilder"/> that can be used to further configure the trace signal.
	/// <para>
	///   <b>Warning:</b> Any further configuration applied via the returned <see cref="TracerProviderBuilder"/> will
	///   be applied <em>after</em> the OTLP exporter has been added.
	/// </para>
	/// <para>
	///   <b>Recommendation:</b>It is strongly recommended to use the <see cref="WithElasticDefaults(TracerProviderBuilder, Action{TracerProviderBuilder})"/>
	///   overload instead. This accepts an <see cref="Action{TracerProviderBuilder}"/> configuration callback and ensures any
	///   customisations are applied before the OTLP exporter is added.
	/// </para>
	/// <para>
	///   Alternatively, the <see cref="ElasticOpenTelemetryOptions.SkipOtlpExporter"/>
	///   <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/configuration#skipotlpexporter">option</see>
	///   automatic addition of the OTLP exporter, allowing you to add it manually at a later stage.
	/// </para>
	/// </returns>
	public static TracerProviderBuilder WithElasticDefaults(this TracerProviderBuilder builder)
	{
#if NET
		ArgumentNullException.ThrowIfNull(builder);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));
#endif

		return WithElasticDefaultsCore(builder, null, null, null, default);
	}

	/// <summary><inheritdoc cref="WithElasticDefaults(TracerProviderBuilder)" /></summary>
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
	///   to further customize the SDK setup. For example, <see cref="HostApplicationBuilderExtensions.AddElasticOpenTelemetry(IHostApplicationBuilder, Action{IOpenTelemetryBuilder})"/>
	///   This ensures that your configuration is invoked after EDOT .NET defaults have been applied, but before the OTLP exporter is added.
	/// </para>
	/// <para>
	///   <b>Note:</b> If you wish to customise the <see cref="TracerProviderBuilder"/> after the EDOT .NET defaults have been applied,
	///   use the <paramref name="configureBuilder"/> action.
	/// </para>
	/// </remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(TracerProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configureBuilder">
	/// An <see cref="Action"/> used to further configure the <see cref="TracerProviderBuilder"/>.
	/// This action is invoked after <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/setup/edot-defaults">EDOT .NET defaults</see>
	/// have been applied, but before the OTLP exporter is added. This ensures that any custom processors run before the exporter.
	/// </param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configureBuilder"/> is null.</exception>
	/// <returns>
	/// <para>
	///   An instance of <see cref="TracerProviderBuilder"/> that can be used to further configure the trace signal.
	/// </para>
	/// <para>
	///   <b>Warning:</b> Any further configuration applied via the returned <see cref="TracerProviderBuilder"/> will
	///   be applied <em>after</em> the OTLP exporter has been added.
	/// </para>
	/// <para>
	///   <b>Recommendation:</b>It is strongly recommended to use the <paramref name="configureBuilder"/> action to customise
	///   the <see cref="TracerProviderBuilder"/> after EDOT .NET defaults are applied and before the OTLP exporter is added.
	/// </para>
	/// </returns>
	public static TracerProviderBuilder WithElasticDefaults(this TracerProviderBuilder builder, Action<TracerProviderBuilder> configureBuilder)
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
		var builderOptions = new BuilderOptions<TracerProviderBuilder> { UserProvidedConfigureBuilder = configureBuilder };
		return WithElasticDefaultsCore(builder, null, null, null, builderOptions);
	}

	/// <summary><inheritdoc cref="WithElasticDefaults(TracerProviderBuilder)" /></summary>
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
	///   <b>Note:</b> If you wish to customise the <see cref="TracerProviderBuilder"/> after the EDOT .NET defaults have been applied,
	///   use the <see cref="WithElasticDefaults(TracerProviderBuilder, ElasticOpenTelemetryOptions, Action{TracerProviderBuilder})"/> overload that accepts a
	///   configuration action.
	/// </para>
	/// </remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(TracerProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="options"><see cref="ElasticOpenTelemetryOptions"/> used to configure the Elastic Distribution of OpenTelemetry (EDOT) .NET.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="options"/> is null.</exception>
	/// <returns>
	/// An instance of <see cref="TracerProviderBuilder"/> that can be used to further configure the trace signal.
	/// <para>
	///   <b>Warning:</b> Any further configuration applied via the returned <see cref="TracerProviderBuilder"/> will
	///   be applied <em>after</em> the OTLP exporter has been added.
	/// </para>
	/// <para>
	///   <b>Recommendation:</b>It is strongly recommended to use the
	///   <see cref="WithElasticDefaults(TracerProviderBuilder, ElasticOpenTelemetryOptions, Action{TracerProviderBuilder})"/>
	///   overload instead. This accepts an <see cref="Action{TracerProviderBuilder}"/> configuration action and ensures any
	///   customisations are applied before the OTLP exporter is added.
	/// </para>
	/// <para>
	///   Alternatively, the <see cref="ElasticOpenTelemetryOptions.SkipOtlpExporter"/>
	///   <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/configuration#skipotlpexporter">option</see>
	///   automatic addition of the OTLP exporter, allowing you to add it manually at a later stage.
	/// </para>
	/// </returns>
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

		return WithElasticDefaultsCore(builder, new(options), null, null, default);
	}

	/// <summary><inheritdoc cref="WithElasticDefaults(TracerProviderBuilder)" /></summary>
	/// <remarks><inheritdoc cref="WithElasticDefaults(TracerProviderBuilder, Action{TracerProviderBuilder})" /></remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(TracerProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="options"><inheritdoc cref="WithElasticDefaults(TracerProviderBuilder, ElasticOpenTelemetryOptions)" path="/param[@name='options']"/></param>
	/// <param name="configureBuilder"><inheritdoc cref="WithElasticDefaults(TracerProviderBuilder, Action{TracerProviderBuilder})" path="/param[@name='configureBuilder']"/></param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="options"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configureBuilder"/> is null.</exception>
	/// <returns><inheritdoc cref="WithElasticDefaults(TracerProviderBuilder, Action{TracerProviderBuilder})" /></returns>
	public static TracerProviderBuilder WithElasticDefaults(this TracerProviderBuilder builder,
		ElasticOpenTelemetryOptions options, Action<TracerProviderBuilder> configureBuilder)
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

		var builderOptions = new BuilderOptions<TracerProviderBuilder> { UserProvidedConfigureBuilder = configureBuilder };
		return WithElasticDefaultsCore(builder, new(options), null, null, builderOptions);
	}

	/// <summary><inheritdoc cref="WithElasticDefaults(TracerProviderBuilder)" /></summary>
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
	///   <b>Note:</b> If you wish to customise the <see cref="TracerProviderBuilder"/> after the EDOT .NET defaults have been applied,
	///   use the <see cref="WithElasticDefaults(TracerProviderBuilder, IConfiguration, Action{TracerProviderBuilder})"/> overload that accepts a
	///   configuration action.
	/// </para>
	/// </remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(TracerProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configuration">An <see cref="IConfiguration"/> instance from which to bind the OpenTelemetry SDK options.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configuration"/> is null.</exception>
	/// <returns>
	/// An instance of <see cref="TracerProviderBuilder"/> that can be used to further configure the trace signal.
	/// <para>
	///   <b>Warning:</b> Any further configuration applied via the returned <see cref="TracerProviderBuilder"/> will
	///   be applied <em>after</em> the OTLP exporter has been added.
	/// </para>
	/// <para>
	///   <b>Recommendation:</b>It is strongly recommended to use the
	///   <see cref="WithElasticDefaults(TracerProviderBuilder, IConfiguration, Action{TracerProviderBuilder})"/>
	///   overload instead. This accepts an <see cref="Action{TracerProviderBuilder}"/> configuration action and ensures any
	///   customisations are applied before the OTLP exporter is added.
	/// </para>
	/// <para>
	///   Alternatively, the <see cref="ElasticOpenTelemetryOptions.SkipOtlpExporter"/>
	///   <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/configuration#skipotlpexporter">option</see>
	///   automatic addition of the OTLP exporter, allowing you to add it manually at a later stage.
	/// </para>
	/// </returns>
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
		return WithElasticDefaultsCore(builder, new(configuration), null, null, default);
	}

	/// <summary><inheritdoc cref="WithElasticDefaults(TracerProviderBuilder)" /></summary>
	/// <remarks><inheritdoc cref="WithElasticDefaults(TracerProviderBuilder, Action{TracerProviderBuilder})" /></remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(TracerProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configuration"><inheritdoc cref="WithElasticDefaults(TracerProviderBuilder, IConfiguration)" path="/param[@name='configuration']"/></param>
	/// <param name="configureBuilder"><inheritdoc cref="WithElasticDefaults(TracerProviderBuilder, Action{TracerProviderBuilder})" path="/param[@name='configureBuilder']"/></param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configuration"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configureBuilder"/> is null.</exception>
	/// <returns><inheritdoc cref="WithElasticDefaults(TracerProviderBuilder, Action{TracerProviderBuilder})" /></returns>
	public static TracerProviderBuilder WithElasticDefaults(this TracerProviderBuilder builder,
		IConfiguration configuration, Action<TracerProviderBuilder> configureBuilder)
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
		var builderOptions = new BuilderOptions<TracerProviderBuilder> { UserProvidedConfigureBuilder = configureBuilder };
		return WithElasticDefaultsCore(builder, new(configuration), null, null, builderOptions);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static TracerProviderBuilder WithElasticDefaultsCore(
		this TracerProviderBuilder builder,
		CompositeElasticOpenTelemetryOptions? options,
		ElasticOpenTelemetryComponents? components,
		IServiceCollection? services,
		in BuilderOptions<TracerProviderBuilder> builderOptions)
	{
		var logger = SignalBuilder.GetLogger(builder, components, options, null);

		var callCount = Interlocked.Increment(ref WithElasticDefaultsCallCount);

		if (callCount > 1)
		{
			logger.LogMultipleWithElasticDefaultsCallsWarning(callCount, nameof(TracerProviderBuilder));
		}
		else
		{
			logger.LogWithElasticDefaultsCallCount(callCount, nameof(TracerProviderBuilder));
		}

		return SignalBuilder.WithElasticDefaults(builder, options, components, services, builderOptions, ConfigureBuilder);
	}

	[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "The call to AssemblyScanning.AddInstrumentationViaReflection` " +
		"is guarded by a RuntimeFeature.IsDynamicCodeSupported` check and, therefore, this method is safe to call in AoT scenarios.")]

	private static void ConfigureBuilder(BuilderContext<TracerProviderBuilder> builderContext)
	{
		var builder = builderContext.Builder;
		var builderState = builderContext.BuilderState;
		var components = builderState.Components;
		var logger = components.Logger;
		var services = builderContext.Services;

		// FullName may return null so we fallback to Name when required.
		var tracerProviderBuilderName = builder.GetType().FullName ?? builder.GetType().Name;

		logger.LogConfiguringBuilder(tracerProviderBuilderName, builderState.InstanceIdentifier);

		builder.ConfigureResource(r => r.WithElasticDefaults(builderState, services));

		// When services is not null here, the options will have already been configured by the calling code so
		// we don't need to do it again.
		if (services is null)
		{
			builder.ConfigureServices(sc => sc.Configure<OtlpExporterOptions>(OtlpExporterDefaults.OtlpExporterOptions));
			logger.LogConfiguredOtlpExporterOptions();
		}

#if NET9_0_OR_GREATER
		// .NET 9 introduced semantic convention compatible instrumentation in System.Net.Http so it's recommended to no longer
		// use the contrib instrumentation. We don't bring in the dependency for .NET 9+. However, if the consuming app depends
		// on it, it will be assumed that the user prefers it and therefore we allow the assembly scanning to add it. We don't
		// add the native source to avoid doubling up on spans.
		if (SignalBuilder.InstrumentationAssemblyExists("OpenTelemetry.Instrumentation.Http.dll"))
		{
			logger.LogHttpInstrumentationFound("trace", tracerProviderBuilderName, builderState.InstanceIdentifier);

			if (!RuntimeFeature.IsDynamicCodeSupported)
				logger.LogWarning("The OpenTelemetry.Instrumentation.Http.dll was found alongside the executing assembly. " +
					"When using Native AOT publishing on .NET, the trace instrumentation is not registered automatically. Either register it manually, " +
					"or remove the dependency so that the native `System.Net.Http` instrumentation (available in .NET 9) is observed instead.");
		}
		else
		{
			TracerProvderBuilderExtensions.AddActivitySourceWithLogging(builder, logger, "System.Net.Http", builderState.InstanceIdentifier);
		}
#endif

		TracerProvderBuilderExtensions.AddActivitySourceWithLogging(builder, logger, "Elastic.Transport", builderState.InstanceIdentifier);

#if NET
		if (RuntimeFeature.IsDynamicCodeSupported)
#endif
		{
			SignalBuilder.AddInstrumentationViaReflection(builder, components, ContribTraceInstrumentation.GetReflectionInstrumentationAssemblies(), builderState.InstanceIdentifier);

			// This is special-cased because we need to register additional options to ensure we capture exceptions by default
			// This improves the UI experience as requests which cause an exception are highlighted in the UI and users can view the
			// log generated from the span event.
			AddAspNetCoreInstrumentation(builder, builderState);
		}

		TracerProvderBuilderExtensions.AddElasticProcessorsCore(builder, builderState, null, services);

		var userProvidedConfigureBuilder = builderContext.BuilderOptions.UserProvidedConfigureBuilder;
		if (userProvidedConfigureBuilder is not null)
		{
			userProvidedConfigureBuilder(builder);
			logger.LogInvokedConfigureAction(tracerProviderBuilderName, builderState.InstanceIdentifier);
		}

		// If a callee will register the OTLP exporter later, we skip adding it here.
		if (builderContext.BuilderOptions.DeferAddOtlpExporter)
		{
			logger.LogDeferredOtlpExporter(tracerProviderBuilderName, builderState.InstanceIdentifier);
		}
		else
		{
			if (components.Options.SkipOtlpExporter)
			{
				logger.LogSkippedOtlpExporter(nameof(Signals.Traces), tracerProviderBuilderName, builderState.InstanceIdentifier);
			}
			else
			{
				builder.AddOtlpExporter();
				logger.LogAddedOtlpExporter(nameof(Signals.Traces), tracerProviderBuilderName, builderState.InstanceIdentifier);
			}
		}

		logger.LogConfiguredSignalProvider(nameof(Signals.Traces), tracerProviderBuilderName, builderState.InstanceIdentifier);
	}

	[UnconditionalSuppressMessage("DynamicCode", "IL2026", Justification = "The call to this method is guarded by a RuntimeFeature.IsDynamicCodeSupported` " +
		"check and therefore this method is safe to call in AoT scenarios.")]
	[UnconditionalSuppressMessage("DynamicCode", "IL3050", Justification = "The call to this method is guarded by a RuntimeFeature.IsDynamicCodeSupported` " +
		"check and therefore this method is safe to call in AoT scenarios.")]
	[UnconditionalSuppressMessage("DynamicallyAccessMembers", "IL2075", Justification = "The call to this method is guarded by a RuntimeFeature.IsDynamicCodeSupported` " +
		"check and therefore this method is safe to call in AoT scenarios.")]
	private static void AddAspNetCoreInstrumentation(TracerProviderBuilder builder, BuilderState builderState)
	{
		if (builderState.Components.Options.SkipInstrumentationAssemblyScanning)
			return;

		var logger = builderState.Components.Logger;

		const string tracerProviderBuilderExtensionsTypeName = "OpenTelemetry.Trace.AspNetCoreInstrumentationTracerProviderBuilderExtensions";
		const string aspNetCoreTraceInstrumentationOptionsTypeName = "OpenTelemetry.Instrumentation.AspNetCore.AspNetCoreTraceInstrumentationOptions";
		const string extensionMethodName = "AddAspNetCoreInstrumentation";
		const string assemblyName = "OpenTelemetry.Instrumentation.AspNetCore";

		var builderTypeName = builder.GetType().Name;

		try
		{
			var tracerProviderBuilderExtensionsType = Type.GetType($"{tracerProviderBuilderExtensionsTypeName}, {assemblyName}");
			var optionsType = Type.GetType($"{aspNetCoreTraceInstrumentationOptionsTypeName}, {assemblyName}");

			if (tracerProviderBuilderExtensionsType is null)
			{
				logger.LogUnableToFindType(tracerProviderBuilderExtensionsTypeName, assemblyName);
				return;
			}

			if (optionsType is null)
			{
				logger.LogUnableToFindType(aspNetCoreTraceInstrumentationOptionsTypeName, assemblyName);
				return;
			}

			Action<object> configureOptions = options =>
			{
				var enrichWithExceptionProperty = options.GetType().GetProperty("EnrichWithException");
				if (enrichWithExceptionProperty is not null)
				{
					var enrichWithExceptionDelegate = (Action<Activity, Exception>)((activity, ex) =>
					{
						activity.AddException(ex);

						if (ex.Source is not null)
						{
							activity.SetTag("exception.source", ex.Source);
						}
					});

					enrichWithExceptionProperty.SetValue(options, enrichWithExceptionDelegate);
				}
			};

			var methodInfo = tracerProviderBuilderExtensionsType.GetMethod(extensionMethodName, BindingFlags.Static | BindingFlags.Public,
				Type.DefaultBinder, [typeof(TracerProviderBuilder), typeof(Action<>).MakeGenericType(optionsType)], null);

			if (methodInfo is null)
			{
				logger.LogUnableToFindMethodWarning(tracerProviderBuilderExtensionsTypeName, extensionMethodName, assemblyName);
				return;
			}

			methodInfo.Invoke(null, [builder, configureOptions]);

			logger.LogAddedInstrumentationViaReflection(assemblyName, builderTypeName, builderState.InstanceIdentifier);
		}
		catch (Exception ex)
		{
			logger.LogError(new EventId(503, "DynamicInstrumentaionFailed"), ex, "Failed to dynamically enable " +
				"{InstrumentationName} on {Provider}.", assemblyName, builderTypeName);
		}
	}
}
