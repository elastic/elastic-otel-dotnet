// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Elastic.OpenTelemetry;
using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Core;
using Elastic.OpenTelemetry.Diagnostics;
using Elastic.OpenTelemetry.Instrumentation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Matching namespace with MeterProviderBuilder
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace OpenTelemetry.Metrics;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods for <see cref="MeterProviderBuilder"/> used to register
/// the Elastic Distribution of OpenTelemetry (EDOT) defaults.
/// </summary>
public static class MeterProviderBuilderExtensions
{
	private static readonly GlobalProviderBuilderState GlobalMeterProviderBuilderState = new();

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
			FullyQualifiedType = "OpenTelemetry.Metrics.AspNetCoreInstrumentationMeterProviderBuilderExtensions",
			InstrumentationMethod = "AddAspNetCoreInstrumentation"
		}
	];

	/// <summary>
	/// Use Elastic Distribution of OpenTelemetry .NET defaults for <see cref="MeterProviderBuilder"/>.
	/// </summary>
	/// <remarks>
	/// This is not neccesary if <see cref="OpenTelemetryBuilderExtensions.WithElasticDefaults(IOpenTelemetryBuilder)"/>
	/// has been called previously as that automatically adds the .
	/// </remarks>
	/// <param name="builder">The <see cref="MeterProviderBuilder"/> to configure.</param>
	/// <returns>The <see cref="MeterProviderBuilder"/> for chaining configuration.</returns>
	public static MeterProviderBuilder UseElasticDefaults(this MeterProviderBuilder builder) =>
		UseElasticDefaultsCore(builder, null, null);

	/// <summary>
	/// <inheritdoc cref="UseElasticDefaults(MeterProviderBuilder)" />
	/// </summary>
	/// <param name="builder"><inheritdoc cref="UseElasticDefaults(MeterProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="skipOtlpExporter">When registering Elastic defaults, skip automatic registration of the OTLP exporter for metrics.</param>
	/// <returns><inheritdoc cref="UseElasticDefaults(MeterProviderBuilder)" /></returns>
	public static MeterProviderBuilder UseElasticDefaults(this MeterProviderBuilder builder, bool skipOtlpExporter) =>
		UseElasticDefaultsCore(builder, skipOtlpExporter ? CompositeElasticOpenTelemetryOptions.SkipOtlpOptions : null, null);

	/// <summary>
	/// <inheritdoc cref="UseElasticDefaults(MeterProviderBuilder)" />
	/// </summary>
	/// <param name="builder"><inheritdoc cref="UseElasticDefaults(MeterProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="options"><see cref="ElasticOpenTelemetryOptions"/> used to configure the Elastic Distribution of OpenTelemetry (EDOT) for .NET.</param>
	/// <returns><inheritdoc cref="UseElasticDefaults(MeterProviderBuilder)" /></returns>
	public static MeterProviderBuilder UseElasticDefaults(this MeterProviderBuilder builder, ElasticOpenTelemetryOptions options)
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
	/// <inheritdoc cref="UseElasticDefaults(MeterProviderBuilder)" />
	/// </summary>
	/// <param name="builder"><inheritdoc cref="UseElasticDefaults(MeterProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configuration">An <see cref="IConfiguration"/> instance from which to load the Elastic Distribution of
	/// OpenTelemetry (EDOT) options.</param>
	/// <returns><inheritdoc cref="UseElasticDefaults(MeterProviderBuilder)" /></returns>
	public static MeterProviderBuilder UseElasticDefaults(this MeterProviderBuilder builder, IConfiguration configuration)
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
	internal static MeterProviderBuilder UseElasticDefaults(
		this MeterProviderBuilder builder,
		ElasticOpenTelemetryComponents components) =>
			UseElasticDefaultsCore(builder, components.Options, components, null);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static MeterProviderBuilder UseElasticDefaults(
		this MeterProviderBuilder builder,
		ElasticOpenTelemetryComponents components,
		IServiceCollection serviceCollection) =>
			UseElasticDefaultsCore(builder, components.Options, components, serviceCollection);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static MeterProviderBuilder UseElasticDefaults(
		this MeterProviderBuilder builder,
		IConfiguration configuration,
		IServiceCollection serviceCollection) =>
			UseElasticDefaultsCore(builder, new(configuration), null, serviceCollection);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static MeterProviderBuilder UseElasticDefaults(
		this MeterProviderBuilder builder,
		IServiceCollection serviceCollection) =>
			UseElasticDefaultsCore(builder, null, null, serviceCollection);

	[RequiresDynamicCode("Requires reflection for dynamic assembly loading and instrumentation activation.")]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static MeterProviderBuilder UseElasticDefaultsCore(
		this MeterProviderBuilder builder,
		CompositeElasticOpenTelemetryOptions? options,
		ElasticOpenTelemetryComponents? components,
		IServiceCollection? services = null)
	{
		try
		{
			if (!SignalBuilder.ConfigureBuilder(nameof(UseElasticDefaults), nameof(MeterProviderBuilder), builder,
				GlobalMeterProviderBuilderState, options, services, ConfigureBuilder, ref components))
			{
				var logger = components?.Logger ?? options?.AdditionalLogger;
				logger?.LogError("Unable to configure {Builder} with Elastic defaults.", nameof(MeterProviderBuilder));
				return builder;
			}
		}
		catch (Exception ex)
		{
			var exceptionLogger = components is not null ? components.Logger : options?.AdditionalLogger;
			exceptionLogger?.LogError(ex, "Failed to fully register EDOT .NET meter defaults for {Provider}.", nameof(MeterProviderBuilder));
		}

		return builder;

		static void ConfigureBuilder(MeterProviderBuilder builder, ElasticOpenTelemetryComponents components)
		{
			builder.ConfigureResource(r => r.AddElasticDistroAttributes());

			AddWithLogging(builder, components.Logger, "HttpClient", b => b.AddHttpClientInstrumentation());
			AddWithLogging(builder, components.Logger, "Process", b => b.AddProcessInstrumentation());

			// TODO - Guard this behind runtime checks e.g. RuntimeFeature.IsDynamicCodeSupported to support AoT users.
			// see https://github.com/elastic/elastic-otel-dotnet/issues/198
			AddInstrumentationViaReflection(builder, components.Logger);

			if (components.Options.SkipOtlpExporter || components.Options.SkipOtlpExporter)
			{
				components.Logger.LogSkippingOtlpExporter(nameof(Signals.Traces), nameof(MeterProviderBuilder));
			}
			else
			{
				builder.AddOtlpExporter();
			}

			components.Logger.LogConfiguredSignalProvider(nameof(Signals.Logs), nameof(MeterProviderBuilder));
		}

		static void AddWithLogging(MeterProviderBuilder builder, ILogger logger, string name, Action<MeterProviderBuilder> add)
		{
			add.Invoke(builder);
			logger.LogAddedInstrumentation(name, nameof(MeterProviderBuilder));
		}

		static void AddInstrumentationViaReflection(MeterProviderBuilder builder, ILogger logger)
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
			catch
			{
				// TODO - Logging
			}
		}

		static void AddInstrumentationLibraryViaReflection(
			MeterProviderBuilder builder,
			ILogger logger,
			string assemblyLocation,
			in InstrumentationAssemblyInfo info)
		{
			try
			{
				var assemblyPath = Path.Combine(assemblyLocation, info.Filename);

				if (File.Exists(Path.Combine(assemblyLocation, info.Filename)))
				{
					logger.LogLocatedInstrumentationAssembly(info.Filename, assemblyLocation);

					var assembly = Assembly.LoadFrom(assemblyPath);
					var type = assembly?.GetType(info.FullyQualifiedType);
					var method = type?.GetMethod(info.InstrumentationMethod, BindingFlags.Static | BindingFlags.Public,
						Type.DefaultBinder, [typeof(MeterProviderBuilder)], null);

					if (method is not null)
					{
						logger.LogAddedInstrumentation(info.Name, nameof(MeterProviderBuilder));
						method.Invoke(null, [builder]);
					}
					else
					{
						logger.LogWarning("Unable to invoke {TypeName}.{Method} on {AssemblyPath}.", info.FullyQualifiedType, info.InstrumentationMethod, assemblyPath);
					}
				}
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Failed to dynamically enable {InstrumentationName} on {Provider}.", info.Name, nameof(MeterProviderBuilder));
			}
		}
	}
}
