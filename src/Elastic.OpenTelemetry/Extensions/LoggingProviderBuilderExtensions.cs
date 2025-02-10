// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Runtime.CompilerServices;
using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Core;
using Elastic.OpenTelemetry.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Matching namespace with LoggerProviderBuilder
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace OpenTelemetry.Logs;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods for <see cref="LoggerProviderBuilder"/> used to register
/// the Elastic Distribution of OpenTelemetry (EDOT) defaults.
/// </summary>
public static class LoggingProviderBuilderExtensions
{
	private static readonly GlobalProviderBuilderState GlobalLoggerProviderBuilderState = new();

	/// <summary>
	/// Use Elastic Distribution of OpenTelemetry (EDOT) defaults for <see cref="LoggerProviderBuilder"/>.
	/// </summary>
	/// <param name="builder">The <see cref="LoggerProviderBuilder"/> to configure.</param>
	/// <returns>The <see cref="LoggerProviderBuilder"/> for chaining configuration.</returns>
	public static LoggerProviderBuilder UseElasticDefaults(this LoggerProviderBuilder builder) =>
		UseElasticDefaultsCore(builder, null, null);

	/// <summary>
	/// <inheritdoc cref="UseElasticDefaults(LoggerProviderBuilder)" />
	/// </summary>
	/// <param name="builder"><inheritdoc cref="UseElasticDefaults(LoggerProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="skipOtlpExporter">When registering Elastic defaults, skip automatic registration of the OTLP exporter for logging.</param>
	/// <returns><inheritdoc cref="UseElasticDefaults(LoggerProviderBuilder)" /></returns>
	public static LoggerProviderBuilder UseElasticDefaults(this LoggerProviderBuilder builder, bool skipOtlpExporter) =>
		UseElasticDefaultsCore(builder, skipOtlpExporter ? CompositeElasticOpenTelemetryOptions.SkipOtlpOptions : CompositeElasticOpenTelemetryOptions.DefaultOptions, null);

	/// <summary>
	/// <inheritdoc cref="UseElasticDefaults(LoggerProviderBuilder)" />
	/// </summary>
	/// <param name="builder"><inheritdoc cref="UseElasticDefaults(LoggerProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="options"><see cref="ElasticOpenTelemetryOptions"/> used to configure the Elastic Distribution of OpenTelemetry (EDOT) for .NET.</param>
	/// <returns><inheritdoc cref="UseElasticDefaults(LoggerProviderBuilder)" /></returns>
	public static LoggerProviderBuilder UseElasticDefaults(this LoggerProviderBuilder builder, ElasticOpenTelemetryOptions options)
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
	/// <inheritdoc cref="UseElasticDefaults(LoggerProviderBuilder)" />
	/// </summary>
	/// <param name="builder"><inheritdoc cref="UseElasticDefaults(LoggerProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configuration">An <see cref="IConfiguration"/> instance from which to load the OpenTelemetry SDK options.</param>
	/// <returns><inheritdoc cref="UseElasticDefaults(LoggerProviderBuilder)" /></returns>
	public static LoggerProviderBuilder UseElasticDefaults(this LoggerProviderBuilder builder, IConfiguration configuration)
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
	internal static LoggerProviderBuilder UseElasticDefaults(this LoggerProviderBuilder builder, ElasticOpenTelemetryComponents components) =>
		UseElasticDefaultsCore(builder, components.Options, components);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static LoggerProviderBuilder UseElasticDefaults(this LoggerProviderBuilder builder, ElasticOpenTelemetryComponents components, IServiceCollection? services) =>
		UseElasticDefaultsCore(builder, components.Options, components, services);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static LoggerProviderBuilder UseElasticDefaultsCore(
		this LoggerProviderBuilder builder,
		CompositeElasticOpenTelemetryOptions? options,
		ElasticOpenTelemetryComponents? components,
		IServiceCollection? services = null)
	{
		var logger = components?.Logger ?? options?.AdditionalLogger;

		try
		{
			if (!SignalBuilder.Configure(nameof(UseElasticDefaults), nameof(LoggerProviderBuilder), builder,
				GlobalLoggerProviderBuilderState, options, services, ConfigureBuilder, ref components))
			{
				logger = components?.Logger ?? options?.AdditionalLogger; // Update with ref-returned components
				logger?.UnableToConfigureLoggingDefaultsError(nameof(LoggerProviderBuilder));
				return builder;
			}
		}
		catch (Exception ex)
		{
			// NOTE: Not using LoggerMessage as we want to pass the exception. As this should be rare, performance isn't critical here.
			logger?.LogError(ex, "Failed to fully register EDOT .NET logging defaults for {Provider}.", nameof(LoggerProviderBuilder));
		}

		return builder;

		static void ConfigureBuilder(LoggerProviderBuilder builder, CompositeElasticOpenTelemetryOptions options, ElasticOpenTelemetryComponents components)
		{
			builder.ConfigureResource(r => r.AddElasticDistroAttributes());

			if (options.SkipOtlpExporter || components.Options.SkipOtlpExporter)
			{
				components.Logger.LogSkippingOtlpExporter(nameof(Signals.Logs), nameof(LoggerProviderBuilder));
			}
			else
			{
				builder.AddOtlpExporter();
			}

			components.Logger.LogConfiguredSignalProvider(nameof(Signals.Logs), nameof(LoggerProviderBuilder));
		}
	}
}
