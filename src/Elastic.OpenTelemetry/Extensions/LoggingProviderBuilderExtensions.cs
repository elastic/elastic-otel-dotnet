// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Runtime.CompilerServices;
using Elastic.OpenTelemetry;
using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Core;
using Elastic.OpenTelemetry.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
	/// <summary>
	/// Used to track the number of times any overload of `UseElasticDefaults` is invoked on a
	/// `LoggingProviderBuilder`. Generally, we expect one builder to be used per application,
	/// and for it to be configured once. By tracking the total count of invocations, we can
	/// log scenarios where the consumer may have inadvertently misconfigured OpenTelemetry in
	/// their application.
	/// </summary>
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
		const string providerBuilderName = nameof(LoggerProviderBuilder);

		var logger = SignalBuilder.GetLogger(components, options);

		// If the signal is disabled via configuration we skip any potential bootstrapping.
		if (!SignalBuilder.IsSignalEnabled(components, options, Signals.Logs, providerBuilderName, logger))
			return builder;

		try
		{
			if (!SignalBuilder.ConfigureBuilder(nameof(UseElasticDefaults), providerBuilderName, builder,
				GlobalLoggerProviderBuilderState, options, services, ConfigureBuilder, ref components))
			{
				logger = components?.Logger ?? options?.AdditionalLogger ?? NullLogger.Instance; // Update the logger we should use from the ref-returned components.
				logger.UnableToConfigureLoggingDefaultsError(providerBuilderName);
				return builder;
			}
		}
		catch (Exception ex)
		{
			// NOTE: Not using LoggerMessage as we want to pass the exception. As this should be rare, performance isn't critical here.
			logger.LogError(ex, "Failed to fully register EDOT .NET logging defaults for {ProviderBuilderType}.", providerBuilderName);
		}

		return builder;

		static void ConfigureBuilder(LoggerProviderBuilder builder, ElasticOpenTelemetryComponents components)
		{
			builder.ConfigureResource(r => r.AddElasticDistroAttributes());

			if (components.Options.SkipOtlpExporter)
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
