// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Reflection;
using System.Runtime.CompilerServices;
using Elastic.OpenTelemetry;
using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Core;
using Elastic.OpenTelemetry.Core.Diagnostics;
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
	// We define these statically for now. One important caveat is that if we add/modify methods, we need to update these accordingly.
	// Since we don't expect to change the public API very often, this is an acceptable trade-off to avoid calculating this at runtime.
	// We could consider a source generator to produce these automatically in the future if needed for all public methods in this class.
	// These are used for diagnostics/logging purposes only.
	private static readonly string ClassName = typeof(ServiceCollectionExtensions).FullName ?? nameof(ServiceCollectionExtensions);
	private static readonly string AddElasticOpenTelemetryMethodNoArgs = $"{ClassName}.{nameof(AddElasticOpenTelemetry)}(this IServiceCollection services)";
	private static readonly string AddElasticOpenTelemetryMethodWithConfigureAction = $"{ClassName}.{nameof(AddElasticOpenTelemetry)}(this IServiceCollection services, Action<IOpenTelemetryBuilder> configure)";
	private static readonly string AddElasticOpenTelemetryMethodWithOptions = $"{ClassName}.{nameof(AddElasticOpenTelemetry)}(this IServiceCollection services, ElasticOpenTelemetryOptions options)";
	private static readonly string AddElasticOpenTelemetryMethodWithOptionsAndConfigureAction = $"{ClassName}.{nameof(AddElasticOpenTelemetry)}" +
		"(this IServiceCollection services, ElasticOpenTelemetryOptions options, Action<IOpenTelemetryBuilder> configure)";
	private static readonly string AddElasticOpenTelemetryMethodWithIConfiguration = $"{ClassName}.{nameof(AddElasticOpenTelemetry)}(this IServiceCollection services, IConfiguration configuration)";
	private static readonly string AddElasticOpenTelemetryMethodWithIConfigurationAndConfigureAction = $"{ClassName}.{nameof(AddElasticOpenTelemetry)}" +
		"(this IServiceCollection services, IConfiguration configuration, Action<IOpenTelemetryBuilder> configure)";
	private static readonly string AddElasticOpenTelemetryMethodWithIConfigurationAndOptions = $"{ClassName}.{nameof(AddElasticOpenTelemetry)}" +
		"(this IServiceCollection services, IConfiguration configuration, ElasticOpenTelemetryOptions options)";

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
		// We don't capture the stack trace here as we'll have that logged deeper in the call stack if needed.
		if (BootstrapLogger.IsEnabled)
			BootstrapLogger.Log($"{AddElasticOpenTelemetryMethodNoArgs} invoked on `IServiceCollection`.");

#if NET
		ArgumentNullException.ThrowIfNull(services);
#else
		if (services is null)
			throw new ArgumentNullException(nameof(services));
#endif

		var builderOptions = new BuilderOptions<IOpenTelemetryBuilder>
		{
			CalleeName = AddElasticOpenTelemetryMethodNoArgs
		};

		return AddElasticOpenTelemetryCore(services, CompositeElasticOpenTelemetryOptions.DefaultOptions, builderOptions);
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

		// We don't capture the stack trace here as we'll have that logged deeper in the call stack if needed.
		if (BootstrapLogger.IsEnabled)
			BootstrapLogger.Log($"{AddElasticOpenTelemetryMethodWithConfigureAction} invoked on `IServiceCollection`.");

#if NET
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);
#else
		if (services is null)
			throw new ArgumentNullException(nameof(services));

		if (configure is null)
			throw new ArgumentNullException(nameof(configure));
#endif

		var builderOptions = new BuilderOptions<IOpenTelemetryBuilder>
		{
			UserProvidedConfigureBuilder = configure,
			CalleeName = AddElasticOpenTelemetryMethodWithConfigureAction
		};

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
		// We don't capture the stack trace here as we'll have that logged deeper in the call stack if needed.
		if (BootstrapLogger.IsEnabled)
			BootstrapLogger.Log($"{AddElasticOpenTelemetryMethodWithOptions} invoked on `IServiceCollection`." +
				$"{Environment.NewLine}    Invoked with `{nameof(ElasticOpenTelemetryOptions)}` instance '{options.InstanceId}'.");

#if NET
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(options);
#else
		if (services is null)
			throw new ArgumentNullException(nameof(services));

		if (options is null)
			throw new ArgumentNullException(nameof(options));
#endif

		var builderOptions = new BuilderOptions<IOpenTelemetryBuilder>
		{
			CalleeName = AddElasticOpenTelemetryMethodWithOptions
		};

		return AddElasticOpenTelemetryCore(services, new(options), builderOptions);
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

		// We don't capture the stack trace here as we'll have that logged deeper in the call stack if needed.
		if (BootstrapLogger.IsEnabled)
			BootstrapLogger.Log($"{AddElasticOpenTelemetryMethodWithOptionsAndConfigureAction} invoked on `IServiceCollection`." +
				$"{Environment.NewLine}    Invoked with `{nameof(ElasticOpenTelemetryOptions)}` instance '{options.InstanceId}'.");

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

		var builderOptions = new BuilderOptions<IOpenTelemetryBuilder>
		{
			UserProvidedConfigureBuilder = configure,
			CalleeName = AddElasticOpenTelemetryMethodWithOptionsAndConfigureAction
		};

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
		// We don't capture the stack trace here as we'll have that logged deeper in the call stack if needed.
		if (BootstrapLogger.IsEnabled)
			BootstrapLogger.Log($"{AddElasticOpenTelemetryMethodWithIConfiguration} invoked on `IServiceCollection`.");

#if NET
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);
#else
		if (services is null)
			throw new ArgumentNullException(nameof(services));

		if (configuration is null)
			throw new ArgumentNullException(nameof(configuration));
#endif

		var builderOptions = new BuilderOptions<IOpenTelemetryBuilder>
		{
			CalleeName = AddElasticOpenTelemetryMethodWithIConfiguration
		};

		return AddElasticOpenTelemetryCore(services, new(configuration), builderOptions);
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

		// We don't capture the stack trace here as we'll have that logged deeper in the call stack if needed.
		if (BootstrapLogger.IsEnabled)
			BootstrapLogger.Log($"{AddElasticOpenTelemetryMethodWithIConfigurationAndConfigureAction} invoked on `IServiceCollection`.");

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

		var builderOptions = new BuilderOptions<IOpenTelemetryBuilder>
		{
			UserProvidedConfigureBuilder = configure,
			CalleeName = AddElasticOpenTelemetryMethodWithIConfigurationAndConfigureAction
		};

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
		// We don't capture the stack trace here as we'll have that logged deeper in the call stack if needed.
		if (BootstrapLogger.IsEnabled)
			BootstrapLogger.Log($"{AddElasticOpenTelemetryMethodWithIConfigurationAndOptions} invoked on `IServiceCollection`." +
				$"{Environment.NewLine}    Invoked with `{nameof(ElasticOpenTelemetryOptions)}` instance '{options.InstanceId}'.");

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

		var builderOptions = new BuilderOptions<IOpenTelemetryBuilder>
		{
			CalleeName = AddElasticOpenTelemetryMethodWithIConfigurationAndOptions
		};

		return AddElasticOpenTelemetryCore(services, new(configuration, options), builderOptions);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static IOpenTelemetryBuilder AddElasticOpenTelemetryCore(
		this IServiceCollection services,
		CompositeElasticOpenTelemetryOptions options,
		in BuilderOptions<IOpenTelemetryBuilder> builderOptions)
	{
		// We don't capture the stack trace here as we'll have that logged deeper in the call stack if needed.
		if (BootstrapLogger.IsEnabled)
		{
			BootstrapLogger.Log($"{nameof(ServiceCollectionExtensions)}.{nameof(AddElasticOpenTelemetryCore)} invoked." +
				$"{Environment.NewLine}    Invoked with `{nameof(CompositeElasticOpenTelemetryOptions)}` instance '{options.InstanceId}'.");
			BootstrapLogger.LogBuilderOptions(builderOptions, nameof(ServiceCollectionExtensions), nameof(AddElasticOpenTelemetryCore));
		}

		var logger = DeferredLogger.GetOrCreate(options);

		logger.LogCallerInfo(builderOptions);

		// From this point on, we skip logging caller info as we have already logged the main caller entrypoint.
		var nextBuilderOptions = builderOptions.SkipLogCallerInfo ? builderOptions : builderOptions with
		{
			SkipLogCallerInfo = true
		};

		services.Configure<OtlpExporterOptions>(OtlpExporterDefaults.OtlpExporterOptions);
		logger.LogConfiguredOtlpExporterOptions("all signals");

		var builder = services.AddOpenTelemetry().WithElasticDefaultsCore(options, nextBuilderOptions);

		if (!services.Any(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(ElasticOpenTelemetryService)))
		{
			services.Insert(0, ServiceDescriptor.Singleton<IHostedService, ElasticOpenTelemetryService>());
		}

		return builder;
	}
}
