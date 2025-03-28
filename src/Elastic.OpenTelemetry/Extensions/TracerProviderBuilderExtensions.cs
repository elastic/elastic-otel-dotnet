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
/// the Elastic Distribution of OpenTelemetry (EDOT) .NET defaults.
/// </summary>
public static class TracerProviderBuilderExtensions
{
	private static int WithElasticDefaultsCallCount;

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

		return WithElasticDefaultsCore(builder, null, null, null);
	}


	/// <summary>
	/// <inheritdoc cref="WithElasticDefaults(TracerProviderBuilder)" />
	/// </summary>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(TracerProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="options"><see cref="ElasticOpenTelemetryOptions"/> used to configure the Elastic Distribution of OpenTelemetry (EDOT) .NET.</param>
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

		return WithElasticDefaultsCore(builder, new(options), null, null);
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
		return WithElasticDefaultsCore(builder, new(configuration), null, null);
	}

	internal static TracerProviderBuilder WithElasticDefaults(
		this TracerProviderBuilder builder,
		IConfiguration configuration,
		IServiceCollection serviceCollection) =>
			WithElasticDefaultsCore(builder, new(configuration), null, serviceCollection);

	internal static TracerProviderBuilder WithElasticDefaults(
		this TracerProviderBuilder builder,
		ElasticOpenTelemetryComponents components,
		IServiceCollection? services) =>
			WithElasticDefaultsCore(builder, components.Options, components, services);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static TracerProviderBuilder WithElasticDefaultsCore(
		TracerProviderBuilder builder,
		CompositeElasticOpenTelemetryOptions? options,
		ElasticOpenTelemetryComponents? components,
		IServiceCollection? services)
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

		return SignalBuilder.WithElasticDefaults(builder, Signals.Traces, options, components, services, ConfigureBuilder);
	}

	[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "The call to AssemblyScanning.AddInstrumentationViaReflection` " +
		"is guarded by a RuntimeFeature.IsDynamicCodeSupported` check and, therefore, this method is safe to call in AoT scenarios.")]

	private static void ConfigureBuilder(TracerProviderBuilder builder, BuilderState builderState, IServiceCollection? services)
	{
		const string tracerProviderBuilderName = nameof(TracerProviderBuilder);

		var components = builderState.Components;
		var logger = components.Logger;

		logger.LogConfiguringBuilder(tracerProviderBuilderName, builderState.InstanceIdentifier);

		builder.ConfigureResource(r => r.WithElasticDefaults(builderState, services));

		if (services is null)
			builder.ConfigureServices(sc => sc.Configure<OtlpExporterOptions>(OtlpExporterDefaults.OtlpExporterOptions));

#if NET9_0_OR_GREATER
		// .NET 9 introduced semantic convention compatible instrumentation in System.Net.Http so it's recommended to no longer
		// use the contrib instrumentation. We don't bring in the dependency for .NET 9+. However, if the consuming app depends
		// on it, it will be assumed that the user prefers it and therefore we allow the assembly scanning to add it. We don't
		// add the native source to avoid doubling up on spans.
		if (!SignalBuilder.InstrumentationAssemblyExists("OpenTelemetry.Instrumentation.Http.dll"))
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

		if (components.Options.SkipOtlpExporter)
		{
			logger.LogSkippingOtlpExporter(nameof(Signals.Traces), nameof(TracerProviderBuilder), builderState.InstanceIdentifier);
		}
		else
		{
			builder.AddOtlpExporter();
		}

		logger.LogConfiguredSignalProvider(nameof(Signals.Traces), nameof(TracerProviderBuilder), builderState.InstanceIdentifier);
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
				logger.LogUnableToFindTypeWarning(tracerProviderBuilderExtensionsTypeName, assemblyName);
				return;
			}

			if (optionsType is null)
			{
				logger.LogUnableToFindTypeWarning(aspNetCoreTraceInstrumentationOptionsTypeName, assemblyName);
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
		}
		catch (Exception ex)
		{
			logger.LogError(new EventId(503, "DynamicInstrumentaionFailed"), ex, "Failed to dynamically enable " +
				"{InstrumentationName} on {Provider}.", assemblyName, builderTypeName);
		}
	}
}
