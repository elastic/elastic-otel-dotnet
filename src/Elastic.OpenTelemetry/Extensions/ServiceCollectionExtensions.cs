// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Runtime.CompilerServices;
using Elastic.OpenTelemetry;
using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Core;
using Elastic.OpenTelemetry.Diagnostics;
using Elastic.OpenTelemetry.Exporters;
using Elastic.OpenTelemetry.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
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
	/// Registers the OpenTelemetry SDK with the application, configured with Elastic Distribution of OpenTelemetry (EDOT) .NET
	/// <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/setup/edot-defaults">defaults</see> for traces,
	/// metrics and logs.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection"/> for adding services.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="services"/> is null.</exception>
	/// <returns>
	/// <para>
	///   An instance of <see cref="IOpenTelemetryBuilder"/> that can be used to further configure the
	///   OpenTelemetry SDK.
	/// </para>
	/// <para>
	///   <b>Warning:</b> Any further configuration applied via the returned <see cref="IOpenTelemetryBuilder"/> will
	///   be applied <em>after</em> the OTLP exporter has been added.
	///   </para>
	/// <para>
	///   <b>Recommendation:</b>It is strongly recommended to use the <see cref="AddElasticOpenTelemetry(IServiceCollection, Action{IOpenTelemetryBuilder})"/> overload
	///   to customise the builder instead. This accepts an <see cref="Action{IOpenTelemetryBuilder}"/> configuration action and ensures any
	///   customisations are applied before the OTLP exporter is added.
	/// </para>
	/// <para>
	///   Alternatively, the <see cref="ElasticOpenTelemetryOptions.SkipOtlpExporter"/>
	///   <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/configuration#skipotlpexporter">option</see>
	///   can be used to prevent automatic addition of the OTLP exporter, allowing you to add it manually at a later stage.
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
	/// <param name="configure">A <see cref="IOpenTelemetryBuilder"/> configuration action used to further customise
	/// the OpenTelemetry SDK after Elastic Distribution of OpenTelemetry (EDOT) .NET defaults have been applied and
	/// before the OTLP exporter is added.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="services"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configure"/> is null.</exception>
	/// <returns>
	/// <para>
	///   An instance of <see cref="IOpenTelemetryBuilder"/> that can be used to further configure the
	///   OpenTelemetry SDK.
	/// </para>
	/// <para>
	///   <b>Warning:</b> Any further configuration applied via the returned <see cref="IOpenTelemetryBuilder"/> will
	///   be applied <em>after</em> the OTLP exporter has been added.
	///   </para>
	/// <para>
	///   <b>Recommendation:</b>It is strongly recommended to use the <paramref name="configure"/> parameter to customise
	///   the builder instead. This accepts an <see cref="Action{IOpenTelemetryBuilder}"/> configuration callback and ensures any
	///   customisations are applied before the OTLP exporter is added.
	/// </para>
	/// <para>
	///   Alternatively, the <see cref="ElasticOpenTelemetryOptions.SkipOtlpExporter"/> <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/configuration#skipotlpexporter">option</see>
	///   can be used to prevent automatic addition of the OTLP exporter, allowing you to add it manually at a later stage.
	/// </para>
	/// </returns>
	public static IOpenTelemetryBuilder AddElasticOpenTelemetry(this IServiceCollection services, Action<IOpenTelemetryBuilder> configure)
	{
		// TODO - Breaking change: In a future major release, rename this parameter to 'configureBuilder' for clarity and consistency.
		// This would be a source breaking change only but we'll reserve it for a major version to avoid disrupting consumers.
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
	/// <para>
	///   An instance of <see cref="IOpenTelemetryBuilder"/> that can be used to further configure the
	///   OpenTelemetry SDK.
	/// </para>
	/// <para>
	///   <b>Warning:</b> Any further configuration applied via the returned <see cref="IOpenTelemetryBuilder"/> will
	///   be applied <em>after</em> the OTLP exporter has been added.
	///   </para>
	/// <para>
	///   <b>Recommendation:</b>It is strongly recommended to use the
	///   <see cref="AddElasticOpenTelemetry(IServiceCollection, ElasticOpenTelemetryOptions, Action{IOpenTelemetryBuilder})"/> overload
	///   to customise the builder instead. This accepts an <see cref="Action{IOpenTelemetryBuilder}"/> configuration action and ensures any
	///   customisations are applied before the OTLP exporter is added.
	/// </para>
	/// <para>
	///   Alternatively, the <see cref="ElasticOpenTelemetryOptions.SkipOtlpExporter"/>
	///   <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/configuration#skipotlpexporter">option</see>
	///   can be used to prevent automatic addition of the OTLP exporter, allowing you to add it manually at a later stage.
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
	/// <param name="services"><inheritdoc cref="AddElasticOpenTelemetry(IServiceCollection)" path="/param[@name='services']"/></param>
	/// <param name="configure"><inheritdoc cref="AddElasticOpenTelemetry(IServiceCollection, Action{IOpenTelemetryBuilder})" path="/param[@name='configure']"/></param>
	/// <param name="options">
	/// The <see cref="CompositeElasticOpenTelemetryOptions"/> used to configure the Elastic Distribution of OpenTelemetry (EDOT) .NET.
	/// </param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="services"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="options"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configure"/> is null.</exception>
	/// <returns>
	/// <para>
	///   An instance of <see cref="IOpenTelemetryBuilder"/> that can be used to further configure the
	///   OpenTelemetry SDK.
	/// </para>
	/// <para>
	///   <b>Warning:</b> Any further configuration applied via the returned <see cref="IOpenTelemetryBuilder"/> will
	///   be applied <em>after</em> the OTLP exporter has been added.
	///   </para>
	/// <para>
	///   <b>Recommendation:</b>It is strongly recommended to use the <paramref name="configure"/> parameter to customise
	///   the builder instead. This accepts an <see cref="Action{IOpenTelemetryBuilder}"/> configuration callback and ensures any
	///   customisations are applied before the OTLP exporter is added.
	/// </para>
	/// <para>
	///   Alternatively, the <see cref="ElasticOpenTelemetryOptions.SkipOtlpExporter"/>
	///   <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/configuration#skipotlpexporter">option</see>
	///   can be used to prevent automatic addition of the OTLP exporter, allowing you to add it manually at a later stage.
	/// </para>
	/// </returns>
	public static IOpenTelemetryBuilder AddElasticOpenTelemetry(this IServiceCollection services,
		ElasticOpenTelemetryOptions options, Action<IOpenTelemetryBuilder> configure)
	{
		// TODO - Breaking change: In a future major release, rename this parameter to 'configureBuilder' for clarity and consistency.
		// This would be a source breaking change only but we'll reserve it for a major version to avoid disrupting consumers.
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
	///   <b>Warning:</b> Any further configuration applied via the returned <see cref="IOpenTelemetryBuilder"/> will
	///   be applied <em>after</em> the OTLP exporter has been added.
	/// </para>
	/// <para>
	///   <b>Recommendation:</b> When not using <c>AddElasticOpenTelemetry</c>, it is strongly recommended to use the
	///   <see cref="AddElasticOpenTelemetry(IServiceCollection, IConfiguration, Action{IOpenTelemetryBuilder})"/> overload instead. This accepts
	///   an <see cref="Action{IOpenTelemetryBuilder}"/> configuration callback and ensures anycustomisations are applied before the OTLP exporter is added.
	/// </para>
	/// <para>
	///   Alternatively, the <see cref="ElasticOpenTelemetryOptions.SkipOtlpExporter"/> <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/configuration#skipotlpexporter">option</see>
	///   can be used to prevent automatic addition of the OTLP exporter, allowing you to add it manually at a later stage.
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
	/// <param name="services"><inheritdoc cref="AddElasticOpenTelemetry(IServiceCollection)" path="/param[@name='services']"/></param>
	/// <param name="configuration"><inheritdoc cref="AddElasticOpenTelemetry(IServiceCollection, IConfiguration)" path="/param[@name='configuration']"/></param>
	/// <param name="configure"><inheritdoc cref="AddElasticOpenTelemetry(IServiceCollection, Action{IOpenTelemetryBuilder})" path="/param[@name='configure']"/></param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="services"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configuration"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configure"/> is null.</exception>
	/// <returns>
	/// <para>
	///   An instance of <see cref="IOpenTelemetryBuilder"/> that can be used to further configure the
	///   OpenTelemetry SDK.
	/// </para>
	/// <para>
	///   <b>Warning:</b> Any further configuration applied via the returned <see cref="IOpenTelemetryBuilder"/> will
	///   be applied <em>after</em> the OTLP exporter has been added.
	///   </para>
	/// <para>
	///   <b>Recommendation:</b>It is strongly recommended to use the <paramref name="configure"/> parameter to customise
	///   the builder instead. This accepts an <see cref="Action{IOpenTelemetryBuilder}"/> configuration callback and ensures any
	///   customisations are applied before the OTLP exporter is added.
	/// </para>
	/// <para>
	///   Alternatively, the <see cref="ElasticOpenTelemetryOptions.SkipOtlpExporter"/>
	///   <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/configuration#skipotlpexporter">option</see>
	///   can be used to prevent automatic addition of the OTLP exporter, allowing you to add it manually at a later stage.
	/// </para>
	/// </returns>
	public static IOpenTelemetryBuilder AddElasticOpenTelemetry(this IServiceCollection services,
		IConfiguration configuration, Action<IOpenTelemetryBuilder> configure)
	{
		// TODO - Breaking change: In a future major release, rename this parameter to 'configureBuilder' for clarity and consistency.
		// This would be a source breaking change only but we'll reserve it for a major version to avoid disrupting consumers.
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

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static IOpenTelemetryBuilder AddElasticOpenTelemetryCore(
		this IServiceCollection services,
		CompositeElasticOpenTelemetryOptions options,
		in BuilderOptions<IOpenTelemetryBuilder> builderOptions)
	{
		var logger = DeferredLogger.GetOrCreate(options);

		services.Configure<OtlpExporterOptions>(OtlpExporterDefaults.OtlpExporterOptions);
		logger.LogConfiguredOtlpExporterOptions();

		var builder = services.AddOpenTelemetry().WithElasticDefaultsCore(options, builderOptions);

		if (!services.Any(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(ElasticOpenTelemetryService)))
		{
			services.Insert(0, ServiceDescriptor.Singleton<IHostedService, ElasticOpenTelemetryService>());
		}

		return builder;
	}
}
