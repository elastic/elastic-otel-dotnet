// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Reflection;
using System.Runtime.CompilerServices;
using Elastic.OpenTelemetry;
using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Core;
using Elastic.OpenTelemetry.Diagnostics;
using Elastic.OpenTelemetry.Exporters;
using Elastic.OpenTelemetry.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;

// ReSharper disable once CheckNamespace
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> used to register
/// the Elastic Distribution of OpenTelemetry (EDOT) .NET defaults.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers the OpenTelemetry SDK with the application via the provided <see cref="IServiceCollection"/>.
	/// The OpenTelemetry SDK is configured with Elastic Distribution of OpenTelemetry (EDOT) .NET defaults.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection"/> for adding services.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="services"/> is null.</exception>
	/// <returns>
	/// An instance of <see cref="IOpenTelemetryBuilder"/> that can be used to further configure the
	/// OpenTelemetry SDK.
	/// <para>
	/// <b>Warning:</b> Any further configuration applied via the returned <see cref="IOpenTelemetryBuilder"/> will
	/// be applied <em>after</em> the OTLP exporter has been added.
	/// </para>
	/// <para>
	/// <b>Recommendation:</b>It is strongly recommended to use the <see cref="AddElasticOpenTelemetry(IServiceCollection, Action{IOpenTelemetryBuilder})"/>
	/// overload instead. This accepts an <see cref="Action{IOpenTelemetryBuilder}"/> configuration callback and ensures any
	/// customisations are applied before the OTLP exporter is added.
	/// </para>
	/// <para>
	/// Alternatively, the <see cref="ElasticOpenTelemetryOptions.SkipOtlpExporter"/> options can be used to prevent
	/// automatic addition of the OTLP exporter, allowing you to add it manually at a later stage.
	/// </para>
	/// </returns>
	public static IOpenTelemetryBuilder AddElasticOpenTelemetry(this IServiceCollection services)
	{
#if NET
		ArgumentNullException.ThrowIfNull(services);
#else
		if (services is null)
			throw new ArgumentNullException(nameof(services));
#endif

		return AddElasticOpenTelemetryCore(services, CompositeElasticOpenTelemetryOptions.DefaultOptions, default);
	}

	/// <summary>
	/// <inheritdoc cref="AddElasticOpenTelemetry(IServiceCollection)" />
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection"/> for adding services.</param>
	/// <param name="configure">A <see cref="IOpenTelemetryBuilder"/> configuration callback used to further customise
	/// the OpenTelemetry SDK after Elastic Distribution of OpenTelemetry (EDOT) .NET defaults have been applied. This
	/// callback is invoked before the OTLP exporter is added.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="services"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configure"/> is null.</exception>
	/// <returns>
	/// An instance of <see cref="IOpenTelemetryBuilder"/> that can be used to further configure the
	/// OpenTelemetry SDK.
	/// <para>
	/// <b>Warning:</b> Any further configuration applied via the returned <see cref="IOpenTelemetryBuilder"/> will
	/// be applied <em>after</em> the OTLP exporter has been added.
	/// </para>
	/// <para>
	/// <b>Recommendation:</b>It is strongly recommended to use the <paramref name="configure"/> parameter to customise
	/// the builder instead. This accepts an <see cref="Action{IOpenTelemetryBuilder}"/> configuration callback and ensures any
	/// customisations are applied before the OTLP exporter is added.
	/// </para>
	/// <para>
	/// Alternatively, the <see cref="ElasticOpenTelemetryOptions.SkipOtlpExporter"/> options can be used to prevent
	/// automatic addition of the OTLP exporter, allowing you to add it manually at a later stage.
	/// </para>
	/// </returns>
	public static IOpenTelemetryBuilder AddElasticOpenTelemetry(this IServiceCollection services, Action<IOpenTelemetryBuilder> configure)
	{
#if NET
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);
#else
		if (services is null)
			throw new ArgumentNullException(nameof(services));

		if (configure is null)
			throw new ArgumentNullException(nameof(configure));
#endif

		var builderOptions = new BuilderOptions<IOpenTelemetryBuilder> { UserProvidedConfigureBuilder = configure };
		return AddElasticOpenTelemetryCore(services, CompositeElasticOpenTelemetryOptions.DefaultOptions, builderOptions);
	}

	/// <summary>
	/// <inheritdoc cref="AddElasticOpenTelemetry(IServiceCollection)"/>
	/// </summary>
	/// <param name="services"><inheritdoc cref="AddElasticOpenTelemetry(IServiceCollection)" path="/param[@name='services']"/></param>
	/// <param name="options">
	/// The <see cref="CompositeElasticOpenTelemetryOptions"/> used to configure the Elastic Distribution of OpenTelemetry (EDOT) .NET.
	/// </param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="services"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="options"/> is null.</exception>
	/// <returns>
	/// An instance of <see cref="IOpenTelemetryBuilder"/> that can be used to further configure the
	/// OpenTelemetry SDK.
	/// <para>
	/// <b>Warning:</b> Any further configuration applied via the returned <see cref="IOpenTelemetryBuilder"/> will
	/// be applied <em>after</em> the OTLP exporter has been added.
	/// </para>
	/// <para>
	/// <b>Recommendation:</b>It is strongly recommended to use the <see cref="AddElasticOpenTelemetry(IServiceCollection, ElasticOpenTelemetryOptions, Action{IOpenTelemetryBuilder})"/>
	/// overload instead. This accepts an <see cref="Action{IOpenTelemetryBuilder}"/> configuration callback and ensures any
	/// customisations are applied before the OTLP exporter is added.
	/// </para>
	/// <para>
	/// Alternatively, the <see cref="ElasticOpenTelemetryOptions.SkipOtlpExporter"/> options can be used to prevent
	/// automatic addition of the OTLP exporter, allowing you to add it manually at a later stage.
	/// </para>
	/// </returns>
	public static IOpenTelemetryBuilder AddElasticOpenTelemetry(this IServiceCollection services, ElasticOpenTelemetryOptions options)
	{
#if NET
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(options);
#else
		if (services is null)
			throw new ArgumentNullException(nameof(services));

		if (options is null)
			throw new ArgumentNullException(nameof(options));
#endif

		return AddElasticOpenTelemetryCore(services, new(options), default);
	}

	/// <summary>
	/// <inheritdoc cref="AddElasticOpenTelemetry(IServiceCollection)" />
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection"/> for adding services.</param>
	/// <param name="configure">A <see cref="IOpenTelemetryBuilder"/> configuration callback used to further customise
	/// the OpenTelemetry SDK after Elastic Distribution of OpenTelemetry (EDOT) .NET defaults have been applied. This
	/// callback is invoked before the OTLP exporter is added.</param>
	/// <param name="options">
	/// The <see cref="CompositeElasticOpenTelemetryOptions"/> used to configure the Elastic Distribution of OpenTelemetry (EDOT) .NET.
	/// </param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="services"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="options"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configure"/> is null.</exception>
	/// <returns>
	/// An instance of <see cref="IOpenTelemetryBuilder"/> that can be used to further configure the
	/// OpenTelemetry SDK.
	/// <para>
	/// <b>Warning:</b> Any further configuration applied via the returned <see cref="IOpenTelemetryBuilder"/> will
	/// be applied <em>after</em> the OTLP exporter has been added.
	/// </para>
	/// <para>
	/// <b>Recommendation:</b>It is strongly recommended to use the <paramref name="configure"/> parameter to customise
	/// the builder instead. This accepts an <see cref="Action{IOpenTelemetryBuilder}"/> configuration callback and ensures any
	/// customisations are applied before the OTLP exporter is added.
	/// </para>
	/// <para>
	/// Alternatively, the <see cref="ElasticOpenTelemetryOptions.SkipOtlpExporter"/> options can be used to prevent
	/// automatic addition of the OTLP exporter, allowing you to add it manually at a later stage.
	/// </para>
	/// </returns>
	public static IOpenTelemetryBuilder AddElasticOpenTelemetry(this IServiceCollection services,
		ElasticOpenTelemetryOptions options, Action<IOpenTelemetryBuilder> configure)
	{
#if NET
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(configure);
#else
		if (services is null)
			throw new ArgumentNullException(nameof(services));

		if (options is null)
			throw new ArgumentNullException(nameof(options));

		if (options is null)
			throw new ArgumentNullException(nameof(configure));
#endif

		var builderOptions = new BuilderOptions<IOpenTelemetryBuilder> { UserProvidedConfigureBuilder = configure };
		return AddElasticOpenTelemetryCore(services, new(options), builderOptions);
	}

	/// <summary>
	/// <inheritdoc cref="AddElasticOpenTelemetry(IServiceCollection)"/>
	/// </summary>
	/// <param name="services"><inheritdoc cref="AddElasticOpenTelemetry(IServiceCollection)" path="/param[@name='services']"/></param>
	/// <param name="configuration">
	/// An <see cref="IConfiguration"/> instance from which to attempt binding of configuration values.
	/// </param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="services"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configuration"/> is null.</exception>
	/// <returns>
	/// An instance of <see cref="IOpenTelemetryBuilder"/> that can be used to further configure the
	/// OpenTelemetry SDK.
	/// <para>
	/// <b>Warning:</b> Any further configuration applied via the returned <see cref="IOpenTelemetryBuilder"/> will
	/// be applied <em>after</em> the OTLP exporter has been added.
	/// </para>
	/// <para>
	/// <b>Recommendation:</b>It is strongly recommended to use the <see cref="AddElasticOpenTelemetry(IServiceCollection, IConfiguration, Action{IOpenTelemetryBuilder})"/>
	/// overload instead. This accepts an <see cref="Action{IOpenTelemetryBuilder}"/> configuration callback and ensures any
	/// customisations are applied before the OTLP exporter is added.
	/// </para>
	/// <para>
	/// Alternatively, the <see cref="ElasticOpenTelemetryOptions.SkipOtlpExporter"/> options can be used to prevent
	/// automatic addition of the OTLP exporter, allowing you to add it manually at a later stage.
	/// </para>
	/// </returns>
	public static IOpenTelemetryBuilder AddElasticOpenTelemetry(this IServiceCollection services, IConfiguration configuration)
	{
#if NET
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);
#else
		if (services is null)
			throw new ArgumentNullException(nameof(services));

		if (configuration is null)
			throw new ArgumentNullException(nameof(configuration));
#endif

		return AddElasticOpenTelemetryCore(services, new(configuration), default);
	}

	/// <summary>
	/// <inheritdoc cref="AddElasticOpenTelemetry(IServiceCollection)" />
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection"/> for adding services.</param>
	/// <param name="configure">A <see cref="IOpenTelemetryBuilder"/> configuration callback used to further customise
	/// the OpenTelemetry SDK after Elastic Distribution of OpenTelemetry (EDOT) .NET defaults have been applied. This
	/// callback is invoked before the OTLP exporter is added.</param>
	/// <param name="configuration">
	/// An <see cref="IConfiguration"/> instance from which to attempt binding of configuration values.
	/// </param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="services"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configuration"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configure"/> is null.</exception>
	/// <returns>
	/// An instance of <see cref="IOpenTelemetryBuilder"/> that can be used to further configure the
	/// OpenTelemetry SDK.
	/// <para>
	/// <b>Warning:</b> Any further configuration applied via the returned <see cref="IOpenTelemetryBuilder"/> will
	/// be applied <em>after</em> the OTLP exporter has been added.
	/// </para>
	/// <para>
	/// <b>Recommendation:</b>It is strongly recommended to use the <paramref name="configure"/> parameter to customise
	/// the builder instead. This accepts an <see cref="Action{IOpenTelemetryBuilder}"/> configuration callback and ensures any
	/// customisations are applied before the OTLP exporter is added.
	/// </para>
	/// <para>
	/// Alternatively, the <see cref="ElasticOpenTelemetryOptions.SkipOtlpExporter"/> options can be used to prevent
	/// automatic addition of the OTLP exporter, allowing you to add it manually at a later stage.
	/// </para>
	/// </returns>
	public static IOpenTelemetryBuilder AddElasticOpenTelemetry(this IServiceCollection services,
		IConfiguration configuration, Action<IOpenTelemetryBuilder> configure)
	{
#if NET
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentNullException.ThrowIfNull(configure);
#else
		if (services is null)
			throw new ArgumentNullException(nameof(services));

		if (configuration is null)
			throw new ArgumentNullException(nameof(configuration));

		if (configure is null)
			throw new ArgumentNullException(nameof(configure));
#endif

		var builderOptions = new BuilderOptions<IOpenTelemetryBuilder> { UserProvidedConfigureBuilder = configure };
		return AddElasticOpenTelemetryCore(services, new(configuration), builderOptions);
	}

	/// <summary>
	/// <inheritdoc cref="AddElasticOpenTelemetry(IServiceCollection)"/>
	/// </summary>
	/// <remarks>
	/// Configuration is first bound from <see cref="IConfiguration"/> and then overridden by any options configured on
	/// the provided <paramref name="options"/>.
	/// </remarks>
	/// <param name="services"><inheritdoc cref="AddElasticOpenTelemetry(IServiceCollection)" path="/param[@name='services']"/></param>
	/// <param name="configuration">An <see cref="IConfiguration"/> instance from which to attempt binding of configuration values.</param>
	/// <param name="options"><see cref="ElasticOpenTelemetryOptions"/> used to configure the Elastic Distribution of OpenTelemetry (EDOT) .NET.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="services"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configuration"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="options"/> is null.</exception>
	/// <returns><inheritdoc cref="AddElasticOpenTelemetry(IServiceCollection)"/></returns>
	internal static IOpenTelemetryBuilder AddElasticOpenTelemetry(this IServiceCollection services,
		IConfiguration configuration, ElasticOpenTelemetryOptions options)
	{
#if NET
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentNullException.ThrowIfNull(options);
#else
		if (services is null)
			throw new ArgumentNullException(nameof(services));

		if (configuration is null)
			throw new ArgumentNullException(nameof(configuration));

		if (options is null)
			throw new ArgumentNullException(nameof(options));
#endif

		return AddElasticOpenTelemetryCore(services, new(configuration, options), default);
	}

	/// <summary>
	/// <inheritdoc cref="AddElasticOpenTelemetry(IServiceCollection)"/>
	/// </summary>
	/// <param name="services"><inheritdoc cref="AddElasticOpenTelemetry(IServiceCollection)" path="/param[@name='services']"/></param>
	/// <param name="configuration">
	/// An <see cref="IConfiguration"/> instance from which to attempt binding of configuration values.
	/// </param>
	/// <param name="builderOptions">TODO</param>
	/// <param name="calleeName">TODO</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="services"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configuration"/> is null.</exception>
	/// <returns><inheritdoc cref="AddElasticOpenTelemetry(IServiceCollection)"/></returns>
	internal static IOpenTelemetryBuilder AddElasticOpenTelemetry(this IServiceCollection services,
		IConfiguration configuration, BuilderOptions<IOpenTelemetryBuilder> builderOptions, string? calleeName = null)
	{
#if NET
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);
#else
		if (services is null)
			throw new ArgumentNullException(nameof(services));

		if (configuration is null)
			throw new ArgumentNullException(nameof(configuration));
#endif

		return AddElasticOpenTelemetryCore(services, new(configuration), builderOptions, calleeName);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static IOpenTelemetryBuilder AddElasticOpenTelemetryCore(
		IServiceCollection services,
		CompositeElasticOpenTelemetryOptions options,
		BuilderOptions<IOpenTelemetryBuilder> builderOptions,
		string? calleeName = null)
	{
		var logger = DeferredLogger.GetOrCreate(options);

		calleeName ??= $"{typeof(ServiceCollectionExtensions).FullName}.{nameof(AddElasticOpenTelemetryCore)}";
		StackTraceHelper.LogCallerInfo(logger, calleeName);

		services.Configure<OtlpExporterOptions>(OtlpExporterDefaults.OtlpExporterOptions);
		logger.LogConfiguredOtlpExporterOptions();

		// TODO - Pass a defer log caller info flag down so that we don't log in the With... methods.
		var builder = services.AddOpenTelemetry().WithElasticDefaultsCore(options, builderOptions);

		if (!services.Any((ServiceDescriptor d) => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(ElasticOpenTelemetryService)))
		{
			services.Insert(0, ServiceDescriptor.Singleton<IHostedService, ElasticOpenTelemetryService>());
		}

		return builder;
	}
}
