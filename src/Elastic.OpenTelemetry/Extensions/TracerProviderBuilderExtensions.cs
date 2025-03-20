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

		if (services is null)
			builder.ConfigureServices(sc => sc.Configure<OtlpExporterOptions>(OtlpExporterDefaults.OtlpExporterOptions));

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
			TracerProvderBuilderExtensions.AddActivitySourceWithLogging(builder, logger, "System.Net.Http", builderState.InstanceIdentifier);
		}
#else
		AddWithLogging(builder, logger, "HTTP", b => b.AddHttpClientInstrumentation(), builderState.InstanceIdentifier);
#endif

		AddWithLogging(builder, logger, "GrpcClient", b => b.AddGrpcClientInstrumentation(), builderState.InstanceIdentifier);
		TracerProvderBuilderExtensions.AddActivitySourceWithLogging(builder, logger, "Elastic.Transport", builderState.InstanceIdentifier);

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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static void AddWithLogging(TracerProviderBuilder builder, ILogger logger, string name, Action<TracerProviderBuilder> add, string builderIdentifier)
		{
			add.Invoke(builder);
			logger.LogAddedInstrumentation(name, nameof(TracerProviderBuilder), builderIdentifier);
		}
	}
}
