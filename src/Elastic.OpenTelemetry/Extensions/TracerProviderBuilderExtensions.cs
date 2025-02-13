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
			Name = "Http",
			Filename = "OpenTelemetry.Instrumentation.Http.dll",
			FullyQualifiedType = "OpenTelemetry.Trace.HttpClientInstrumentationTracerProviderBuilderExtensions",
			InstrumentationMethod = "AddHttpClientInstrumentation"
		},
#endif
	];

	/// <summary>
	/// Use Elastic Distribution of OpenTelemetry .NET defaults for <see cref="TracerProviderBuilder"/>.
	/// </summary>
	/// <remarks>
	/// This is not neccesary if <see cref="OpenTelemetryBuilderExtensions.WithElasticDefaults(IOpenTelemetryBuilder)"/>
	/// has been called previously as that automatically adds the .
	/// </remarks>
	/// <param name="builder">The <see cref="TracerProviderBuilder"/> to configure.</param>
	/// <returns>The <see cref="TracerProviderBuilder"/> for chaining configuration.</returns>
	public static TracerProviderBuilder UseElasticDefaults(this TracerProviderBuilder builder) =>
		UseElasticDefaultsCore(builder, null, null);

	/// <summary>
	/// <inheritdoc cref="UseElasticDefaults(TracerProviderBuilder)" />
	/// </summary>
	/// <param name="builder"><inheritdoc cref="UseElasticDefaults(TracerProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="skipOtlpExporter">When registering Elastic defaults, skip automatic registration of the OTLP exporter for traces.</param>
	/// <returns><inheritdoc cref="UseElasticDefaults(TracerProviderBuilder)" /></returns>
	public static TracerProviderBuilder UseElasticDefaults(this TracerProviderBuilder builder, bool skipOtlpExporter) =>
		UseElasticDefaultsCore(builder, skipOtlpExporter ? CompositeElasticOpenTelemetryOptions.SkipOtlpOptions : null, null);

	/// <summary>
	/// <inheritdoc cref="UseElasticDefaults(TracerProviderBuilder)" />
	/// </summary>
	/// <param name="builder"><inheritdoc cref="UseElasticDefaults(TracerProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="options"><see cref="ElasticOpenTelemetryOptions"/> used to configure the Elastic Distribution of OpenTelemetry (EDOT) for .NET.</param>
	/// <returns><inheritdoc cref="UseElasticDefaults(TracerProviderBuilder)" /></returns>
	public static TracerProviderBuilder UseElasticDefaults(this TracerProviderBuilder builder, ElasticOpenTelemetryOptions options)
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
	/// <inheritdoc cref="UseElasticDefaults(TracerProviderBuilder)" />
	/// </summary>
	/// <param name="builder"><inheritdoc cref="UseElasticDefaults(TracerProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configuration">An <see cref="IConfiguration"/> instance from which to load the OpenTelemetry SDK options.</param>
	/// <returns><inheritdoc cref="UseElasticDefaults(TracerProviderBuilder)" /></returns>
	public static TracerProviderBuilder UseElasticDefaults(this TracerProviderBuilder builder, IConfiguration configuration)
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

	[RequiresDynamicCode("Requires reflection for dynamic assembly loading and instrumentation activation.")]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static TracerProviderBuilder UseElasticDefaultsCore(
		TracerProviderBuilder builder,
		CompositeElasticOpenTelemetryOptions? options,
		ElasticOpenTelemetryComponents? components,
		IServiceCollection? services = null)
	{
		try
		{
			if (!SignalBuilder.ConfigureBuilder(nameof(UseElasticDefaults), nameof(TracerProviderBuilder), builder,
				GlobalTracerProviderBuilderState, options, services, ConfigureBuilder, ref components))
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

		static void ConfigureBuilder(TracerProviderBuilder builder, ElasticOpenTelemetryComponents components)
		{
			builder.ConfigureResource(r => r.AddElasticDistroAttributes());

#if NET9_0_OR_GREATER
			try
			{
				// This first check determines whether OpenTelemetry.Instrumentation.Http.dll is present, in which case,
				// it will be registered on the builder via reflection. If it's not present, we can safely add the native
				// source which is OTel compliant since .NET 9.
				var assemblyLocation = Path.GetDirectoryName(typeof(ElasticOpenTelemetry).Assembly.Location);
				if (assemblyLocation is not null)
				{
					var assemblyPath = Path.Combine(assemblyLocation, "OpenTelemetry.Instrumentation.Http.dll");

					if (!File.Exists(assemblyPath))
					{
						AddWithLogging(builder, components.Logger, "Http (via native instrumentation)", b => b.AddSource("System.Net.Http"));
					}
					else
					{
						components.Logger.LogHttpInstrumentationFound(assemblyPath, "trace");
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

			AddWithLogging(builder, components.Logger, "GrpcClient", b => b.AddGrpcClientInstrumentation());
			AddWithLogging(builder, components.Logger, "EntityFrameworkCore", b => b.AddEntityFrameworkCoreInstrumentation());
			AddWithLogging(builder, components.Logger, "NEST", b => b.AddElasticsearchClientInstrumentation());
			AddWithLogging(builder, components.Logger, "SqlClient", b => b.AddSqlClientInstrumentation());
			AddWithLogging(builder, components.Logger, "ElasticTransport", b => b.AddSource("Elastic.Transport"));

			// TODO - Guard this behind runtime checks e.g. RuntimeFeature.IsDynamicCodeSupported to support AoT users.
			// see https://github.com/elastic/elastic-otel-dotnet/issues/198
			AddInstrumentationViaReflection(builder, components.Logger);

			AddElasticProcessorsCore(builder, components);

			if (components.Options.SkipOtlpExporter || components.Options.SkipOtlpExporter)
			{
				components.Logger.LogSkippingOtlpExporter(nameof(Signals.Traces), nameof(TracerProviderBuilder));
			}
			else
			{
				builder.AddOtlpExporter();
			}

			components.Logger.LogConfiguredSignalProvider(nameof(Signals.Traces), nameof(TracerProviderBuilder));
		}

		static void AddWithLogging(TracerProviderBuilder builder, ILogger logger, string name, Action<TracerProviderBuilder> add)
		{
			add.Invoke(builder);
			logger.LogAddedInstrumentation(name, nameof(TracerProviderBuilder));
		}

		static void AddInstrumentationViaReflection(TracerProviderBuilder builder, ILogger logger)
		{
			try
			{
				// This section is in its own try/catch because we don't want failures in the reflection-based
				// registration to prevent completion of registering the more general defaults we apply.

				var assemblyLocation = Path.GetDirectoryName(typeof(ElasticOpenTelemetry).Assembly.Location);
				if (assemblyLocation is not null)
				{
					foreach (var assembly in GetReflectionInstrumentationAssemblies())
						AddInstrumentationLibraryViaReflection(builder, logger, assemblyLocation, assembly);
				}
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "An exception occurred while adding instrumentation via reflection.");
			}
		}

		static void AddInstrumentationLibraryViaReflection(
			TracerProviderBuilder builder,
			ILogger logger,
			string assemblyLocation,
			in InstrumentationAssemblyInfo info)
		{
			try
			{
				var assemblyPath = Path.Combine(assemblyLocation, info.Filename);
				if (File.Exists(assemblyPath))
				{
					logger.LogLocatedInstrumentationAssembly(info.Filename, assemblyLocation);

					var assembly = Assembly.LoadFrom(assemblyPath);
					var type = assembly?.GetType(info.FullyQualifiedType);
					var method = type?.GetMethod(info.InstrumentationMethod, BindingFlags.Static | BindingFlags.Public,
						Type.DefaultBinder, [typeof(TracerProviderBuilder)], null);

					if (method is not null)
					{
						logger.LogAddedInstrumentation(info.Name, nameof(TracerProviderBuilder));
						method.Invoke(null, [builder]);
					}
				}
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Failed to dynamically enable {InstrumentationName} on {Provider}.", info.Name, nameof(TracerProviderBuilder));
			}
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
	/// <returns>The <see cref="TracerProviderBuilder"/> for chaining.</returns>
	public static TracerProviderBuilder AddElasticProcessors(this TracerProviderBuilder builder) =>
		AddElasticProcessorsCore(builder, null);

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

		static void ConfigureBuilder(TracerProviderBuilder builder, ElasticOpenTelemetryComponents components)
		{
			builder.LogAndAddProcessor(new ElasticCompatibilityProcessor(components.Logger), components.Logger);
		}
	}

	private static TracerProviderBuilder LogAndAddProcessor(this TracerProviderBuilder builder, BaseProcessor<Activity> processor, ILogger logger)
	{
		builder.AddProcessor(processor);
		logger.LogProcessorAdded(processor.GetType().ToString(), builder.GetType().Name);
		return builder;
	}
}
