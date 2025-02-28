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
using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

// Matching namespace with MeterProviderBuilder
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace OpenTelemetry.Trace;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides extension methods on the <see cref="TracerProviderBuilder"/> used to register
/// the Elastic Distribution of OpenTelemetry (EDOT) .NET defaults.
/// </summary>
public static class TracerProviderBuilderExtensions
{
	private static int WithElasticDefaultsCallCount;
	private static int AddElasticProcessorsCallCount;

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

		return WithElasticDefaultsCore(builder, skipOtlpExporter ? CompositeElasticOpenTelemetryOptions.SkipOtlpOptions : null, null, null);
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
		var callCount = Interlocked.Increment(ref WithElasticDefaultsCallCount);

		var logger = components?.Logger ?? options?.AdditionalLogger;

		if (logger is null && ElasticOpenTelemetry.BuilderStateTable.TryGetValue(builder, out var state))
			logger = state.Components.Logger;

		if (callCount > 1)
		{
			logger?.LogMultipleWithElasticDefaultsCallsWarning(callCount, nameof(TracerProviderBuilder));
		}
		else
		{
			logger?.LogWithElasticDefaultsCallCount(callCount, nameof(TracerProviderBuilder));
		}

		return SignalBuilder.WithElasticDefaults(builder, Signals.Traces, options, components, services, ConfigureBuilder);
	}

	[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "The calls to `AddSqlClientInstrumentation` and " +
		"`AssemblyScanning.AddInstrumentationViaReflection` are guarded by a RuntimeFeature.IsDynamicCodeSupported` check and therefore " +
		"this method is safe to call in AoT scenarios.")]
	private static void ConfigureBuilder(TracerProviderBuilder builder, BuilderState builderState, IServiceCollection? services)
	{
		const string tracerProviderBuilderName = nameof(TracerProviderBuilder);

		var components = builderState.Components;
		var logger = components.Logger;

		logger.LogConfiguringBuilder(tracerProviderBuilderName, builderState.InstanceIdentifier);

		builder.ConfigureResource(r => r.WithElasticDefaults(builderState, services));

#if NET9_0_OR_GREATER
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
			AddActivitySourceWithLogging(builder, logger, "System.Net.Http", builderState.InstanceIdentifier);
		}
#else
		AddWithLogging(builder, logger, "HTTP", b => b.AddHttpClientInstrumentation(), builderState.InstanceIdentifier);
#endif

		AddWithLogging(builder, logger, "GrpcClient", b => b.AddGrpcClientInstrumentation(), builderState.InstanceIdentifier);
		AddActivitySourceWithLogging(builder, logger, "Elastic.Transport", builderState.InstanceIdentifier);

		// NOTE: Despite them having no dependencies. We cannot add the OpenTelemetry.Instrumentation.ElasticsearchClient or
		// OpenTelemetry.Instrumentation.EntityFrameworkCore instrumentations here, as including the package references causes
		// trimming warnings. We can still add them via reflection.

#if NET
		if (RuntimeFeature.IsDynamicCodeSupported)
#endif
		{
			// This instrumentation is not currently compatible for AoT scenarios.
			AddWithLogging(builder, logger, "SqlClient", b => b.AddSqlClientInstrumentation(), builderState.InstanceIdentifier);
			SignalBuilder.AddInstrumentationViaReflection(builder, components, ContribTraceInstrumentation.GetReflectionInstrumentationAssemblies(), builderState.InstanceIdentifier);
		}

		AddElasticProcessorsCore(builder, builderState, null, services);

		if (components.Options.SkipOtlpExporter)
		{
			logger.LogSkippingOtlpExporter(nameof(Signals.Traces), nameof(TracerProviderBuilder), builderState.InstanceIdentifier);
		}
		else
		{
			builder.AddOtlpExporter();
		}

		logger.LogConfiguredSignalProvider(nameof(Signals.Traces), nameof(TracerProviderBuilder), builderState.InstanceIdentifier);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static void AddWithLogging(TracerProviderBuilder builder, ILogger logger, string name, Action<TracerProviderBuilder> add, string builderIdentifier)
		{
			add.Invoke(builder);
			logger.LogAddedInstrumentation(name, nameof(TracerProviderBuilder), builderIdentifier);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void AddActivitySourceWithLogging(TracerProviderBuilder builder, ILogger logger, string activitySource, string builderIdentifier)
	{
		builder.AddSource(activitySource);
		logger.LogActivitySourceAdded(activitySource, builderIdentifier);
	}

	// We use a different method here to ensure we don't cause a crash depending on instrumentation libraries which are not present.
	// We can't assume that any DLLs are available besides OpenTelemetry.dll, which auto-instrumentation includes.
	// The auto instrumentation enables a set of default instrumentation of it's own, so we rely on that.
	// In the future, we can assess if we should copy instrumentation DLLs into the autoinstrumentation zip file and enable them.
	internal static TracerProviderBuilder UseAutoInstrumentationElasticDefaults(this TracerProviderBuilder builder, ElasticOpenTelemetryComponents components)
	{
		var logger = components.Logger;

		try
		{
			builder.ConfigureResource(r => r.WithElasticDefaults(components, null));
			AddActivitySourceWithLogging(builder, components.Logger, "Elastic.Transport", "<n/a>");
			AddElasticProcessorsCore(builder, null, components, null);

			if (components.Options.SkipOtlpExporter)
			{
				logger.LogSkippingOtlpExporter(nameof(Signals.Traces), nameof(TracerProviderBuilder), "<n/a>");
			}
			else
			{
				builder.AddOtlpExporter();
			}

			logger.LogConfiguredSignalProvider("Traces", nameof(TracerProviderBuilder), "<n/a>");

			return builder;
		}
		catch (Exception ex)
		{
			logger.LogError(new EventId(520, "AutoInstrumentationTracerFailure"), ex,
				"Failed to register EDOT defaults for tracing auto-instrumentation to the TracerProviderBuilder.");
		}

		return builder;
	}

	/// <summary>
	/// Include Elastic trace processors for best compatibility with Elastic Observability.
	/// </summary>
	/// <remarks>
	/// <para>It is not neccessary to call this method if `WithElasticDefaults` has already been called.</para>
	/// <para>Calling this method also adds Elastic defaults to the resource builder.</para>
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

		return AddElasticProcessorsCore(builder, null, null, null);
	}

	/// <summary>
	/// An advanced API to include Elastic Distribution of OpenTelemtry (EDOT) .NET trace processors for best compatibility with
	/// Elastic Observability. Generally, prefer using `WithElasticDefaults` instead, which registers default trace instrumentation.
	/// </summary>
	/// <remarks>
	/// <para>It is not neccessary to call this method if `WithElasticDefaults` has already been called.</para>
	/// <para>Calling this method also bootstraps the Elastic Distribution of OpenTelemtry (EDOT) .NET for logging and configuration
	/// and adds Elastic defaults to the resource builder.</para>
	/// </remarks>
	/// <param name="builder">The <see cref="TracerProviderBuilder"/> where the Elastic trace
	/// processors should be added.</param>
	/// <param name="options"><see cref="ElasticOpenTelemetryOptions"/> used to configure the Elastic Distribution of OpenTelemetry (EDOT) .NET.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="options"/> is null.</exception>
	/// <returns>The <see cref="TracerProviderBuilder"/> for chaining.</returns>
	public static TracerProviderBuilder AddElasticProcessors(this TracerProviderBuilder builder, ElasticOpenTelemetryOptions options)
	{
#if NET
		ArgumentNullException.ThrowIfNull(builder);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));
#endif

		return AddElasticProcessorsCore(builder, null, null, null);
	}

	private static TracerProviderBuilder AddElasticProcessorsCore(
		TracerProviderBuilder builder,
		BuilderState? builderState,
		ElasticOpenTelemetryComponents? components,
		IServiceCollection? services)
	{
		components ??= builderState?.Components;

		var callCount = Interlocked.Increment(ref AddElasticProcessorsCallCount);

		if (callCount > 1)
		{
			var logger = components?.Logger;

			if (logger is null && ElasticOpenTelemetry.BuilderStateTable.TryGetValue(builder, out var state))
				logger = state.Components.Logger;

			logger?.LogMultipleAddElasticProcessorsCallsWarning(callCount);
		}

		if (builderState is not null)
		{
			// When we have existing builderState, this method is being invoked from the main WithElasticDefaults method.
			// In that scenario, we skip configuring the resource, as it will have already been configured by the caller.
			ConfigureBuilderProcessors(builder, builderState, services);
			return builder;
		}

		return SignalBuilder.WithElasticDefaults(builder, Signals.Traces, components?.Options, components, null, ConfigureBuilder);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static void ConfigureBuilder(TracerProviderBuilder builder, BuilderState builderState, IServiceCollection? services)
		{
			builder.ConfigureResource(r => r.WithElasticDefaults(builderState, services));
			builder.LogAndAddProcessor(new ElasticCompatibilityProcessor(builderState.Components.Logger), builderState);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static void ConfigureBuilderProcessors(TracerProviderBuilder builder, BuilderState builderState, IServiceCollection? services)
		{
			builder.LogAndAddProcessor(new ElasticCompatibilityProcessor(builderState.Components.Logger), builderState);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static TracerProviderBuilder LogAndAddProcessor(this TracerProviderBuilder builder, BaseProcessor<Activity> processor, BuilderState builderState)
	{
		builder.AddProcessor(processor);
		builderState.Components.Logger.LogProcessorAdded(processor.GetType().ToString(), builderState.InstanceIdentifier);
		return builder;
	}
}
