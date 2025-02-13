// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Runtime.CompilerServices;
using Elastic.OpenTelemetry;
using Elastic.OpenTelemetry.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenTelemetry;

// ReSharper disable once CheckNamespace
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> used to register
/// the Elastic Distribution of OpenTelemetry (EDOT) defaults.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers the OpenTelemetry SDK services with the provided <see cref="IServiceCollection"/> to include the
	/// OpenTelemetry SDK in the application, configured with Elastic Distribution of OpenTelemetry (EDOT) defaults.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection"/> for adding services.</param>
	/// <returns>
	/// An instance of <see cref="IOpenTelemetryBuilder"/> that can be used to further configure the
	/// OpenTelemetry SDK.
	/// </returns>
	public static IOpenTelemetryBuilder AddElasticOpenTelemetry(this IServiceCollection services) =>
		AddElasticOpenTelemetryCore(services, CompositeElasticOpenTelemetryOptions.DefaultOptions);

	/// <summary>
	/// <inheritdoc cref="AddElasticOpenTelemetry(IServiceCollection)"/>
	/// </summary>
	/// <param name="services"><inheritdoc cref="AddElasticOpenTelemetry(IServiceCollection)" path="/param[@name='services']"/></param>
	/// <param name="skipOtlpExporter">Controls whether the OTLP exporter is enabled automatically.</param>
	/// <returns><inheritdoc cref="AddElasticOpenTelemetry(IServiceCollection)"/></returns>
	public static IOpenTelemetryBuilder AddElasticOpenTelemetry(this IServiceCollection services, bool skipOtlpExporter) =>
		AddElasticOpenTelemetryCore(services, skipOtlpExporter ? CompositeElasticOpenTelemetryOptions.SkipOtlpOptions : CompositeElasticOpenTelemetryOptions.DefaultOptions);

	/// <summary>
	/// <inheritdoc cref="AddElasticOpenTelemetry(IServiceCollection)"/>
	/// </summary>
	/// <param name="services"><inheritdoc cref="AddElasticOpenTelemetry(IServiceCollection)" path="/param[@name='services']"/></param>
	/// <param name="configuration">
	/// An <see cref="IConfiguration"/> instance from which to attempt binding of configuration values.
	/// </param>
	/// <returns><inheritdoc cref="AddElasticOpenTelemetry(IServiceCollection)"/></returns>
	public static IOpenTelemetryBuilder AddElasticOpenTelemetry(this IServiceCollection services, IConfiguration configuration)
	{
#if NET
		ArgumentNullException.ThrowIfNull(configuration);
#else
		if (configuration is null)
			throw new ArgumentNullException(nameof(configuration));
#endif

		return AddElasticOpenTelemetryCore(services, new(configuration));
	}

	/// <summary>
	/// <inheritdoc cref="AddElasticOpenTelemetry(IServiceCollection)"/>
	/// </summary>
	/// <param name="services"><inheritdoc cref="AddElasticOpenTelemetry(IServiceCollection)" path="/param[@name='services']"/></param>
	/// <param name="configuration"><inheritdoc cref="AddElasticOpenTelemetry(IServiceCollection)" path="/param[@name='configuration']"/></param>
	/// <param name="additionalLoggerFactory">
	/// An <see cref="ILoggerFactory"/> that Elastic Distribution of OpenTelemetry (EDOT) can use to create an additional <see cref="ILogger"/>
	/// used for diagnostic logging.
	/// </param>
	/// <returns><inheritdoc cref="AddElasticOpenTelemetry(IServiceCollection)"/></returns>
	public static IOpenTelemetryBuilder AddElasticOpenTelemetry(this IServiceCollection services,
		IConfiguration configuration, ILoggerFactory additionalLoggerFactory)
	{
#if NET
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentNullException.ThrowIfNull(additionalLoggerFactory);
#else
		if (configuration is null)
			throw new ArgumentNullException(nameof(configuration));
		if (additionalLoggerFactory is null)
			throw new ArgumentNullException(nameof(additionalLoggerFactory));
#endif

		return AddElasticOpenTelemetryCore(services, new(configuration, additionalLoggerFactory));
	}

	/// <summary>
	/// <inheritdoc cref="AddElasticOpenTelemetry(IServiceCollection)"/>
	/// </summary>
	/// <param name="services"><inheritdoc cref="AddElasticOpenTelemetry(IServiceCollection)" path="/param[@name='services']"/></param>
	/// <param name="configuration"><inheritdoc cref="AddElasticOpenTelemetry(IServiceCollection)" path="/param[@name='configuration']"/></param>
	/// <param name="additionalLogger">An additional <see cref="ILogger"/> to be used for diagnostic logging.</param>
	/// <returns><inheritdoc cref="AddElasticOpenTelemetry(IServiceCollection)"/></returns>
	public static IOpenTelemetryBuilder AddElasticOpenTelemetry(this IServiceCollection services,
		IConfiguration configuration, ILogger additionalLogger)
	{
#if NET
		ArgumentNullException.ThrowIfNull(configuration);
#else
		if (configuration is null)
			throw new ArgumentNullException(nameof(configuration));
#endif

		return AddElasticOpenTelemetryCore(services, new(configuration) { AdditionalLogger = additionalLogger });
	}

	/// <summary>
	/// <inheritdoc cref="AddElasticOpenTelemetry(IServiceCollection)"/>
	/// </summary>
	/// <param name="services"><inheritdoc cref="AddElasticOpenTelemetry(IServiceCollection)" path="/param[@name='services']"/></param>
	/// <param name="options">
	/// The <see cref="CompositeElasticOpenTelemetryOptions"/> used to configure the Elastic Distribution of OpenTelemetry (EDOT) for .NET.
	/// </param>
	/// <returns><inheritdoc cref="AddElasticOpenTelemetry(IServiceCollection)"/></returns>
	public static IOpenTelemetryBuilder AddElasticOpenTelemetry(this IServiceCollection services, ElasticOpenTelemetryOptions options) =>
		AddElasticOpenTelemetryCore(services, new(options));

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static IOpenTelemetryBuilder AddElasticOpenTelemetryCore(IServiceCollection services, CompositeElasticOpenTelemetryOptions options)
	{
#if NET
		ArgumentNullException.ThrowIfNull(options);
#else
		if (options is null)
			throw new ArgumentNullException(nameof(options));
#endif

		return services.AddOpenTelemetry().WithElasticDefaultsCore(options);
	}
}
