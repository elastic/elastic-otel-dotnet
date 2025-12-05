// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Runtime.CompilerServices;
using Elastic.OpenTelemetry;
using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Core;
using Elastic.OpenTelemetry.Core.Diagnostics;
using Elastic.OpenTelemetry.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

// Matching namespace with OpenTelemetryBuilder
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace OpenTelemetry;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides extension methods on the <see cref="IOpenTelemetryBuilder"/>
/// used to register the Elastic Distribution of OpenTelemetry (EDOT) .NET
/// <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/setup/edot-defaults">defaults</see>.
/// </summary>
public static class OpenTelemetryBuilderExtensions
{
	// We define these statically for now. One important caveat is that if we add/modify methods, we need to update these accordingly.
	// Since we don't expect to change the public API very often, this is an acceptable trade-off to avoid calculating this at runtime.
	// We could consider a source generator to produce these automatically in the future if needed for all public methods in this class.
	// These are used for diagnostics/logging purposes only.
	private static readonly string ClassName = typeof(OpenTelemetryBuilderExtensions).FullName ?? nameof(OpenTelemetryBuilderExtensions);
	private static readonly string WithElasticDefaultsMethodNoArgs = $"{ClassName}.{nameof(WithElasticDefaults)}(this IOpenTelemetryBuilder builder)";
	private static readonly string WithElasticDefaultsMethodWithConfigureBuilderAction = $"{ClassName}.{nameof(WithElasticDefaults)}(this IOpenTelemetryBuilder builder, Action<IOpenTelemetryBuilder> configureBuilder)";
	private static readonly string WithElasticDefaultsMethodWithIConfiguration = $"{ClassName}.{nameof(WithElasticDefaults)}(this IOpenTelemetryBuilder builder, IConfiguration configuration)";
	private static readonly string WithElasticDefaultsMethodWithIConfigurationAndConfigureBuilderAction = $"{ClassName}.{nameof(WithElasticDefaults)}" +
		"(this IOpenTelemetryBuilder builder, IConfiguration configuration, Action<IOpenTelemetryBuilder> configureBuilder)";
	private static readonly string WithElasticDefaultsMethodWithOptions = $"{ClassName}.{nameof(WithElasticDefaults)}(this IOpenTelemetryBuilder builder, ElasticOpenTelemetryOptions options)";
	private static readonly string WithElasticDefaultsMethodWithOptionsAndConfigureBuilderAction = $"{ClassName}.{nameof(WithElasticDefaults)}" +
		"(this IOpenTelemetryBuilder builder, ElasticOpenTelemetryOptions options, Action<IOpenTelemetryBuilder> configureBuilder)";
	private static readonly string WithElasticLoggingMethodNoArgs = $"{ClassName}.{nameof(WithElasticLogging)}(this IOpenTelemetryBuilder builder)";
	private static readonly string WithElasticLoggingMethodWithConfigureAction = $"{ClassName}.{nameof(WithElasticLogging)}(this IOpenTelemetryBuilder builder, Action<LoggerProviderBuilder> configure)";
	private static readonly string WithElasticMetricsMethodNoArgs = $"{ClassName}.{nameof(WithElasticMetrics)}(this IOpenTelemetryBuilder builder)";
	private static readonly string WithElasticMetricsMethodWithConfigureAction = $"{ClassName}.{nameof(WithElasticMetrics)}(this IOpenTelemetryBuilder builder, Action<MeterProviderBuilder> configure)";
	private static readonly string WithElasticMetricsMethodWithIConfiguration = $"{ClassName}.{nameof(WithElasticMetrics)}(this IOpenTelemetryBuilder builder, IConfiguration configuration)";
	private static readonly string WithElasticMetricsMethodWithIConfigurationAndConfigureAction = $"{ClassName}.{nameof(WithElasticMetrics)}" +
		"(this IOpenTelemetryBuilder builder, IConfiguration configuration, Action<MeterProviderBuilder> configure)";
	private static readonly string WithElasticTracingMethodNoArgs = $"{ClassName}.{nameof(WithElasticTracing)}(this IOpenTelemetryBuilder builder)";
	private static readonly string WithElasticTracingMethodWithConfigureAction = $"{ClassName}.{nameof(WithElasticTracing)}(this IOpenTelemetryBuilder builder, Action<TracerProviderBuilder> configure)";
	private static readonly string WithElasticTracingMethodWithIConfiguration = $"{ClassName}.{nameof(WithElasticTracing)}(this IOpenTelemetryBuilder builder, IConfiguration configuration)";
	private static readonly string WithElasticTracingMethodWithIConfigurationAndConfigureAction = $"{ClassName}.{nameof(WithElasticTracing)}" +
		"(this IOpenTelemetryBuilder builder, IConfiguration configuration, Action<TracerProviderBuilder> configure)";

	/// <summary>
	/// Used to track the number of times any variation of `WithElasticDefaults` is invoked by consuming
	/// code across all <see cref="IOpenTelemetryBuilder"/> instances. This allows us to warn about potential
	/// misconfigurations.
	/// </summary>
	private static int WithElasticDefaultsCallCount;

	/// <summary>
	/// Enables collection of all signals using Elastic Distribution of OpenTelemetry .NET
	/// <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/setup/edot-defaults">defaults</see>.
	/// </summary>
	/// <remarks>
	/// <para>
	///   <b>Guidance:</b> Prefer using the <c>AddElasticOpenTelemetry</c> method on the <see cref="IHostApplicationBuilder"/>
	///   or <see cref="IServiceCollection"/> rather than calling this method on the <see cref="IOpenTelemetryBuilder"/> directly.
	/// </para>
	/// <para>
	///   The <c>WithElasticDefaults</c> methods are primarily intended for advanced scenarios, non-host-based applications or when developing
	///   libraries that need to customize the OpenTelemetry configuration.
	/// </para>
	/// <para>
	///   <b>Recommendation:</b>When not using <c>AddElasticOpenTelemetry</c>, it is strongly recommended to use the
	///   <see cref="WithElasticDefaults(IOpenTelemetryBuilder, Action{IOpenTelemetryBuilder})"/> overload instead. This accepts
	///   an <see cref="Action{IOpenTelemetryBuilder}"/> configuration action and ensures any customisations are applied before
	///   the OTLP exporter is added.
	/// </para>
	/// </remarks>
	/// <param name="builder">The <see cref="IOpenTelemetryBuilder"/> being configured.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
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
	///   <b>Recommendation:</b>It is strongly recommended to use the <see cref="WithElasticDefaults(IOpenTelemetryBuilder, Action{IOpenTelemetryBuilder})"/> overload
	///   to customise the builder instead. This accepts an <see cref="Action{IOpenTelemetryBuilder}"/> configuration action and ensures any
	///   customisations are applied before the OTLP exporter is added.
	/// </para>
	/// <para>
	///   Alternatively, the <see cref="ElasticOpenTelemetryOptions.SkipOtlpExporter"/> <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/configuration#skipotlpexporter">option</see>
	///   can be used to prevent automatic addition of the OTLP exporter, allowing you to add it manually at a later stage.
	/// </para>
	/// </returns>
	public static IOpenTelemetryBuilder WithElasticDefaults(this IOpenTelemetryBuilder builder)
	{
		// We don't capture the stack trace here as we'll have that logged deeper in the call stack if needed.
		if (BootstrapLogger.IsEnabled)
			BootstrapLogger.Log($"{WithElasticDefaultsMethodNoArgs} invoked on builder with object hash '{RuntimeHelpers.GetHashCode(builder)}'.");

#if NET
		ArgumentNullException.ThrowIfNull(builder);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));
#endif
		var builderOptions = new BuilderOptions<IOpenTelemetryBuilder>
		{
			CalleeName = WithElasticDefaultsMethodNoArgs
		};

		return WithElasticDefaultsCore(builder, CompositeElasticOpenTelemetryOptions.DefaultOptions, builderOptions);
	}

	/// <summary><inheritdoc cref="WithElasticDefaults(IOpenTelemetryBuilder)" /></summary>
	/// <remarks>
	/// <para>
	///   <b>Guidance:</b> Prefer using the <c>AddElasticOpenTelemetry</c> method on the <see cref="IHostApplicationBuilder"/>
	///   or <see cref="IServiceCollection"/> rather than calling this method on the <see cref="IOpenTelemetryBuilder"/> directly.
	/// </para>
	/// <para>
	///   The <c>WithElasticDefaults</c> methods are primarily intended for advanced scenarios, non-host-based applications or when developing
	///   libraries that need to customize the OpenTelemetry configuration.
	/// </para>
	/// </remarks>
	/// <param name="builder">The <see cref="IOpenTelemetryBuilder"/> being configured.</param>
	/// <param name="configureBuilder">A <see cref="IOpenTelemetryBuilder"/> configuration action used to further customise
	/// the OpenTelemetry SDK after Elastic Distribution of OpenTelemetry (EDOT) .NET defaults have been applied. This
	/// callback is invoked before the OTLP exporter is added.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configureBuilder"/> is null.</exception>
	/// The supplied <see cref="IOpenTelemetryBuilder"/> for chaining calls.
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
	///   <b>Recommendation:</b>It is strongly recommended to use the <paramref name="configureBuilder"/> parameter to customise
	///   the builder instead. This accepts an <see cref="Action{IOpenTelemetryBuilder}"/> configuration action and ensures any
	///   customisations are applied before the OTLP exporter is added.
	/// </para>
	/// <para>
	///   Alternatively, the <see cref="ElasticOpenTelemetryOptions.SkipOtlpExporter"/> <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/configuration#skipotlpexporter">option</see>
	///   can be used to prevent automatic addition of the OTLP exporter, allowing you to add it manually at a later stage.
	/// </para>
	/// </returns>
	public static IOpenTelemetryBuilder WithElasticDefaults(this IOpenTelemetryBuilder builder, Action<IOpenTelemetryBuilder> configureBuilder)
	{
		// We don't capture the stack trace here as we'll have that logged deeper in the call stack if needed.
		if (BootstrapLogger.IsEnabled)
			BootstrapLogger.Log($"{WithElasticDefaultsMethodWithConfigureBuilderAction} invoked on builder with object hash '{RuntimeHelpers.GetHashCode(builder)}'.");

#if NET
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configureBuilder);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (configureBuilder is null)
			throw new ArgumentNullException(nameof(configureBuilder));
#endif
		var builderOptions = new BuilderOptions<IOpenTelemetryBuilder>
		{
			UserProvidedConfigureBuilder = configureBuilder,
			CalleeName = WithElasticDefaultsMethodWithConfigureBuilderAction
		};

		return WithElasticDefaultsCore(builder, CompositeElasticOpenTelemetryOptions.DefaultOptions, builderOptions);
	}

	/// <summary><inheritdoc cref="WithElasticDefaults(IOpenTelemetryBuilder)" /></summary>
	/// <remarks>
	/// <para>
	///   <b>Guidance:</b> Prefer using the <c>AddElasticOpenTelemetry</c> method on the <see cref="IHostApplicationBuilder"/>
	///   or <see cref="IServiceCollection"/> rather than calling this method on the <see cref="IOpenTelemetryBuilder"/> directly.
	/// </para>
	/// <para>
	///   The <c>WithElasticDefaults</c> methods are primarily intended for advanced scenarios, non-host-based applications or when developing
	///   libraries that need to customize the OpenTelemetry configuration.
	/// </para>
	/// <para>
	///   <b>Recommendation:</b>When not using <c>AddElasticOpenTelemetry</c>, it is strongly recommended to use the
	///   <see cref="WithElasticDefaults(IOpenTelemetryBuilder, IConfiguration, Action{IOpenTelemetryBuilder})"/> overload instead. This accepts
	///   an <see cref="Action{IOpenTelemetryBuilder}"/> configuration callback and ensures any customisations are applied before
	///   the OTLP exporter is added.
	/// </para>
	/// </remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(IOpenTelemetryBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configuration">An <see cref="IConfiguration"/> from which to attempt binding of Elastic Distribution of OpenTelemetry
	/// (EDOT) options.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configuration"/> is null.</exception>
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
	///   <b>Recommendation:</b>It is strongly recommended to use the <see cref="WithElasticDefaults(IOpenTelemetryBuilder, IConfiguration, Action{IOpenTelemetryBuilder})"/> overload
	///   to customise the builder instead. This accepts an <see cref="Action{IOpenTelemetryBuilder}"/> configuration callback and ensures any
	///   customisations are applied before the OTLP exporter is added.
	/// </para>
	/// <para>
	///   Alternatively, the <see cref="ElasticOpenTelemetryOptions.SkipOtlpExporter"/> <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/configuration#skipotlpexporter">option</see>
	///   can be used to prevent automatic addition of the OTLP exporter, allowing you to add it manually at a later stage.
	/// </para>
	/// </returns>
	public static IOpenTelemetryBuilder WithElasticDefaults(this IOpenTelemetryBuilder builder, IConfiguration configuration)
	{
		// We don't capture the stack trace here as we'll have that logged deeper in the call stack if needed.
		if (BootstrapLogger.IsEnabled)
			BootstrapLogger.Log($"{WithElasticDefaultsMethodWithIConfiguration} invoked on builder with object hash '{RuntimeHelpers.GetHashCode(builder)}'.");

#if NET
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (configuration is null)
			throw new ArgumentNullException(nameof(configuration));
#endif
		var builderOptions = new BuilderOptions<IOpenTelemetryBuilder>
		{
			CalleeName = WithElasticDefaultsMethodWithIConfiguration
		};

		return WithElasticDefaultsCore(builder, new(configuration), builderOptions);
	}

	/// <summary><inheritdoc cref="WithElasticDefaults(IOpenTelemetryBuilder)" /></summary>
	/// <remarks><inheritdoc cref="WithElasticDefaults(IOpenTelemetryBuilder, Action{IOpenTelemetryBuilder})" /></remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(IOpenTelemetryBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configuration"><inheritdoc cref="WithElasticDefaults(IOpenTelemetryBuilder, IConfiguration)" path="/param[@name='configuration']"/></param>
	/// <param name="configureBuilder"><inheritdoc cref="WithElasticDefaults(IOpenTelemetryBuilder, Action{IOpenTelemetryBuilder})" path="/param[@name='configureBuilder']"/></param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configuration"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configureBuilder"/> is null.</exception>
	/// <returns><inheritdoc cref="WithElasticDefaults(IOpenTelemetryBuilder, Action{IOpenTelemetryBuilder})" /></returns>
	public static IOpenTelemetryBuilder WithElasticDefaults(this IOpenTelemetryBuilder builder,
		IConfiguration configuration, Action<IOpenTelemetryBuilder> configureBuilder)
	{
		// We don't capture the stack trace here as we'll have that logged deeper in the call stack if needed.
		if (BootstrapLogger.IsEnabled)
			BootstrapLogger.Log($"{WithElasticDefaultsMethodWithIConfigurationAndConfigureBuilderAction} invoked on builder with object hash '{RuntimeHelpers.GetHashCode(builder)}'.");

#if NET
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentNullException.ThrowIfNull(configureBuilder);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (configuration is null)
			throw new ArgumentNullException(nameof(configuration));

		if (configureBuilder is null)
			throw new ArgumentNullException(nameof(configureBuilder));
#endif
		var builderOptions = new BuilderOptions<IOpenTelemetryBuilder>
		{
			UserProvidedConfigureBuilder = configureBuilder,
			CalleeName = WithElasticDefaultsMethodWithIConfigurationAndConfigureBuilderAction
		};

		return WithElasticDefaultsCore(builder, new(configuration), builderOptions);
	}

	/// <summary>
	/// <inheritdoc cref="WithElasticDefaults(IOpenTelemetryBuilder)" />
	/// </summary>
	/// <remarks>
	/// <para>
	///   <b>Guidance:</b> Prefer using the <c>AddElasticOpenTelemetry</c> method on the <see cref="IHostApplicationBuilder"/>
	///   or <see cref="IServiceCollection"/> rather than calling this method on the <see cref="IOpenTelemetryBuilder"/> directly.
	/// </para>
	/// <para>
	///   The <c>WithElasticDefaults</c> methods are primarily intended for advanced scenarios, non-host-based applications or when developing
	///   libraries that need to customize the OpenTelemetry configuration.
	/// </para>
	/// <para>
	///   <b>Recommendation:</b>When not using <c>AddElasticOpenTelemetry</c>, it is strongly recommended to use the
	///   <see cref="WithElasticDefaults(IOpenTelemetryBuilder, ElasticOpenTelemetryOptions, Action{IOpenTelemetryBuilder})"/> overload instead. This accepts
	///   an <see cref="Action{IOpenTelemetryBuilder}"/> configuration callback and ensures any customisations are applied before
	///   the OTLP exporter is added.
	/// </para>
	/// </remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(IOpenTelemetryBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="options">
	/// An <see cref="ElasticOpenTelemetryOptions"/> instance used to configure the initial Elastic Distribution of OpenTelemetry (EDOT) .NET defaults.
	/// Note that only the first use for a given <see cref="IOpenTelemetryBuilder"/> instance applies the options. Subsequent builder methods may
	/// accept <see cref="ElasticOpenTelemetryOptions"/> but those will not be reapplied.
	/// </param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="options"/> is null.</exception>
	/// <returns>
	/// <para>
	///   An instance of <see cref="IOpenTelemetryBuilder"/> that can be used to further configure the
	///   OpenTelemetry SDK.
	/// </para>
	/// <para>
	///   <b>Warning:</b> Any further configuration applied via the returned <see cref="IOpenTelemetryBuilder"/> by calling <c>WithLogging</c>,
	///   <c>WithTracing</c> or <c>WithMetrics</c> will be applied <em>after</em> the OTLP exporter has been added.
	///   </para>
	/// <para>
	///   <b>Recommendation:</b>It is strongly recommended to use the <see cref="WithElasticDefaults(IOpenTelemetryBuilder, ElasticOpenTelemetryOptions, Action{IOpenTelemetryBuilder})"/>
	///   overload to customise the builder instead. This accepts an <see cref="Action{IOpenTelemetryBuilder}"/> configuration callback and ensures any
	///   customisations are applied before the OTLP exporter is added.
	/// </para>
	/// <para>
	///   Alternatively, the <see cref="ElasticOpenTelemetryOptions.SkipOtlpExporter"/> <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/configuration#skipotlpexporter">option</see>
	///   can be used to prevent automatic addition of the OTLP exporter, allowing you to add it manually at a later stage.
	/// </para>
	/// </returns>
	public static IOpenTelemetryBuilder WithElasticDefaults(this IOpenTelemetryBuilder builder, ElasticOpenTelemetryOptions options)
	{
		// We don't capture the stack trace here as we'll have that logged deeper in the call stack if needed.
		if (BootstrapLogger.IsEnabled)
			BootstrapLogger.Log($"{WithElasticDefaultsMethodWithOptions} invoked on builder with object hash '{RuntimeHelpers.GetHashCode(builder)}'.");

#if NET
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(options);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (options is null)
			throw new ArgumentNullException(nameof(options));
#endif
		var builderOptions = new BuilderOptions<IOpenTelemetryBuilder>
		{
			CalleeName = WithElasticDefaultsMethodWithOptions
		};

		return WithElasticDefaultsCore(builder, new(options), builderOptions);
	}

	/// <summary><inheritdoc cref="WithElasticDefaults(IOpenTelemetryBuilder)" /></summary>
	/// <remarks><inheritdoc cref="WithElasticDefaults(IOpenTelemetryBuilder, Action{IOpenTelemetryBuilder})" /></remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(IOpenTelemetryBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="options"><inheritdoc cref="WithElasticDefaults(IOpenTelemetryBuilder, ElasticOpenTelemetryOptions)" path="/param[@name='options']"/></param>
	/// <param name="configureBuilder"><inheritdoc cref="WithElasticDefaults(IOpenTelemetryBuilder, Action{IOpenTelemetryBuilder})" path="/param[@name='configureBuilder']"/></param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="options"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configureBuilder"/> is null.</exception>
	/// <returns><inheritdoc cref="WithElasticDefaults(IOpenTelemetryBuilder, Action{IOpenTelemetryBuilder})" /></returns>
	public static IOpenTelemetryBuilder WithElasticDefaults(this IOpenTelemetryBuilder builder,
		ElasticOpenTelemetryOptions options, Action<IOpenTelemetryBuilder> configureBuilder)
	{
		// We don't capture the stack trace here as we'll have that logged deeper in the call stack if needed.
		if (BootstrapLogger.IsEnabled)
			BootstrapLogger.Log($"{WithElasticDefaultsMethodWithOptionsAndConfigureBuilderAction} invoked on builder with object hash '{RuntimeHelpers.GetHashCode(builder)}'.");

#if NET
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(configureBuilder);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (options is null)
			throw new ArgumentNullException(nameof(options));

		if (configureBuilder is null)
			throw new ArgumentNullException(nameof(configureBuilder));
#endif
		var builderOptions = new BuilderOptions<IOpenTelemetryBuilder>
		{
			UserProvidedConfigureBuilder = configureBuilder,
			CalleeName = WithElasticDefaultsMethodWithOptionsAndConfigureBuilderAction
		};

		return WithElasticDefaultsCore(builder, new(options), builderOptions);
	}

	/// <summary>
	/// Adds logging services into the <see cref="IOpenTelemetryBuilder"/> using Elastic Distribution of
	/// OpenTelemetry (EDOT) .NET <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/setup/edot-defaults">defaults</see>.
	/// </summary>
	/// <remarks>
	/// <para>
	///   <b>Guidance:</b> Prefer using the <c>AddElasticOpenTelemetry</c> method on the <see cref="IHostApplicationBuilder"/>
	///   or <see cref="IServiceCollection"/> rather than calling this method on the <see cref="IOpenTelemetryBuilder"/> directly.
	/// </para>
	/// <para>
	///   The <c>WithElasticLogging</c> methods are primarily intended for advanced scenarios, non-host-based applications or when developing
	///   libraries that need to customize the OpenTelemetry configuration.
	/// </para>
	/// <para>
	///   <b>Recommendation:</b>When not using <c>AddElasticOpenTelemetry</c>, it is strongly recommended to use the
	///   <see cref="WithElasticLogging(IOpenTelemetryBuilder, Action{LoggerProviderBuilder})"/> overload instead. This accepts
	///   an <see cref="Action{LoggerProviderBuilder}"/> configuration callback and ensures any customisations are applied before
	///   the OTLP exporter is added.
	/// </para>
	/// </remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(IOpenTelemetryBuilder)" path="/param[@name='builder']"/></param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <returns>
	/// <para>
	///   An instance of <see cref="IOpenTelemetryBuilder"/> that can be used to further configure the
	///   OpenTelemetry SDK.
	/// </para>
	/// <para>
	///   <b>Warning:</b> Any further configuration applied via the returned <see cref="IOpenTelemetryBuilder"/> by calling <c>WithLogging</c> will
	///   be applied <em>after</em> the OTLP exporter has been added to the <see cref="LoggerProviderBuilder"/>.
	///   </para>
	/// <para>
	///   <b>Recommendation:</b>It is strongly recommended to use the <see cref="WithElasticLogging(IOpenTelemetryBuilder, Action{LoggerProviderBuilder})"/> overload
	///   to customise the builder instead. This accepts an <see cref="Action{LoggerProviderBuilder}"/> configuration callback and ensures any
	///   customisations are applied before the OTLP exporter is added.
	/// </para>
	/// <para>
	///   Alternatively, the <see cref="ElasticOpenTelemetryOptions.SkipOtlpExporter"/> <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/configuration#skipotlpexporter">option</see>
	///   can be used to prevent automatic addition of the OTLP exporter, allowing you to add it manually at a later stage.
	/// </para>
	/// </returns>
	public static IOpenTelemetryBuilder WithElasticLogging(this IOpenTelemetryBuilder builder)
	{
		// We don't capture the stack trace here as we'll have that logged deeper in the call stack if needed.
		if (BootstrapLogger.IsEnabled)
			BootstrapLogger.Log($"{WithElasticLoggingMethodNoArgs} invoked on builder with object hash '{RuntimeHelpers.GetHashCode(builder)}'.");

#if NET
		ArgumentNullException.ThrowIfNull(builder);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));
#endif
		var builderOptions = new BuilderOptions<LoggerProviderBuilder>
		{
			CalleeName = WithElasticLoggingMethodNoArgs
		};

		return builder.WithLogging(lpb => lpb.WithElasticDefaultsCore(null, null, builder.Services, builderOptions));
	}

	/// <summary><inheritdoc cref="WithElasticLogging(IOpenTelemetryBuilder)" /></summary>
	/// <remarks>
	/// <para>
	///   <b>Guidance:</b> Prefer using the <c>AddElasticOpenTelemetry</c> method on the <see cref="IHostApplicationBuilder"/>
	///   or <see cref="IServiceCollection"/> rather than calling this method on the <see cref="IOpenTelemetryBuilder"/> directly.
	/// </para>
	/// <para>
	///   The <c>WithElasticLogging</c> methods are primarily intended for advanced scenarios, non-host-based applications or when developing
	///   libraries that need to customize the OpenTelemetry configuration.
	/// </para>
	/// </remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(IOpenTelemetryBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configure">
	/// An <see cref="Action"/> used to further configure the <see cref="LoggerProviderBuilder"/>.
	/// This action is invoked after <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/setup/edot-defaults">EDOT .NET defaults</see>
	/// have been applied, but before the OTLP exporter is added. This ensures that any custom processors run before the exporter.
	/// </param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configure"/> is null.</exception>
	/// <returns>
	/// <para>
	///   An instance of <see cref="IOpenTelemetryBuilder"/> that can be used to further configure the
	///   OpenTelemetry SDK.
	/// </para>
	/// <para>
	///   <b>Warning:</b> Any further configuration applied via the returned <see cref="IOpenTelemetryBuilder"/> by calling <c>WithLogging</c> will
	///   be applied <em>after</em> the OTLP exporter has been added to the <see cref="LoggerProviderBuilder"/>. Use the
	///   <paramref name="configure"/> parameter to apply customisations before the exporter is added.
	/// </para>
	/// </returns>
	public static IOpenTelemetryBuilder WithElasticLogging(this IOpenTelemetryBuilder builder, Action<LoggerProviderBuilder> configure)
	{
		// TODO - Breaking change: In a future major release, rename this parameter to 'configureBuilder' for clarity and consistency.
		// This would be a source breaking change only but we'll reserve it for a major version to avoid disrupting consumers.

		// We don't capture the stack trace here as we'll have that logged deeper in the call stack if needed.
		if (BootstrapLogger.IsEnabled)
			BootstrapLogger.Log($"{WithElasticLoggingMethodWithConfigureAction} invoked on builder with object hash '{RuntimeHelpers.GetHashCode(builder)}'.");

#if NET
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (configure is null)
			throw new ArgumentNullException(nameof(configure));
#endif
		var builderOptions = new BuilderOptions<LoggerProviderBuilder>
		{
			UserProvidedConfigureBuilder = configure,
			CalleeName = WithElasticLoggingMethodWithConfigureAction
		};

		return builder.WithLogging(lpb => lpb.WithElasticDefaultsCore(null, null, builder.Services, builderOptions));
	}

	/// <summary>
	/// Adds metrics services into the <see cref="IOpenTelemetryBuilder"/> using Elastic Distribution of
	/// OpenTelemetry (EDOT) .NET <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/setup/edot-defaults">defaults</see>.
	/// </summary>
	/// <remarks>
	/// <para>
	///   <b>Guidance:</b> Prefer using the <c>AddElasticOpenTelemetry</c> method on the <see cref="IHostApplicationBuilder"/>
	///   or <see cref="IServiceCollection"/> rather than calling this method on the <see cref="IOpenTelemetryBuilder"/> directly.
	/// </para>
	/// <para>
	///   The <c>WithElasticMetrics</c> methods are primarily intended for advanced scenarios, non-host-based applications or when developing
	///   libraries that need to customize the OpenTelemetry configuration.
	/// </para>
	/// <para>
	///   <b>Recommendation:</b>When not using <c>AddElasticOpenTelemetry</c>, it is strongly recommended to use the
	///   <see cref="WithElasticMetrics(IOpenTelemetryBuilder, Action{MeterProviderBuilder})"/> overload instead. This accepts
	///   an <see cref="Action{MeterProviderBuilder}"/> configuration callback and ensures any customisations are applied before
	///   the OTLP exporter is added.
	/// </para>
	/// </remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(IOpenTelemetryBuilder)" path="/param[@name='builder']"/></param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <returns>
	/// <para>
	///   An instance of <see cref="IOpenTelemetryBuilder"/> that can be used to further configure the OpenTelemetry SDK.
	/// </para>
	/// <para>
	///   <b>Warning:</b> Any further configuration applied via the returned <see cref="IOpenTelemetryBuilder"/> by calling <c>WithMetrics</c> will
	///   be applied <em>after</em> the OTLP exporter has been added to the <see cref="MeterProviderBuilder"/>.
	///   </para>
	/// <para>
	///   <b>Recommendation:</b>It is strongly recommended to use the <see cref="WithElasticMetrics(IOpenTelemetryBuilder, Action{MeterProviderBuilder})"/> overload
	///   to customise the builder instead. This accepts an <see cref="Action{MeterProviderBuilder}"/> configuration callback and ensures any
	///   customisations are applied before the OTLP exporter is added.
	/// </para>
	/// <para>
	///   Alternatively, the <see cref="ElasticOpenTelemetryOptions.SkipOtlpExporter"/> <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/configuration#skipotlpexporter">option</see>
	///   can be used to prevent automatic addition of the OTLP exporter, allowing you to add it manually at a later stage.
	/// </para>
	/// </returns>
	public static IOpenTelemetryBuilder WithElasticMetrics(this IOpenTelemetryBuilder builder)
	{
		// We don't capture the stack trace here as we'll have that logged deeper in the call stack if needed.
		if (BootstrapLogger.IsEnabled)
			BootstrapLogger.Log($"{WithElasticMetricsMethodNoArgs} invoked on builder with object hash '{RuntimeHelpers.GetHashCode(builder)}'.");

#if NET
		ArgumentNullException.ThrowIfNull(builder);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));
#endif
		var builderOptions = new BuilderOptions<MeterProviderBuilder>
		{
			CalleeName = WithElasticMetricsMethodNoArgs
		};

		return builder.WithMetrics(mpb => mpb.WithElasticDefaultsCore(null, null, builder.Services, builderOptions));
	}

	/// <summary><inheritdoc cref="WithElasticMetrics(IOpenTelemetryBuilder)" /></summary>
	/// <remarks>
	/// <para>
	///   <b>Guidance:</b> Prefer using the <c>AddElasticOpenTelemetry</c> method on the <see cref="IHostApplicationBuilder"/>
	///   or <see cref="IServiceCollection"/> rather than calling this method on the <see cref="IOpenTelemetryBuilder"/> directly.
	/// </para>
	/// <para>
	///   The <c>WithElasticMetrics</c> methods are primarily intended for advanced scenarios, non-host-based applications or when developing
	///   libraries that need to customize the OpenTelemetry configuration.
	/// </para>
	/// </remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(IOpenTelemetryBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configure">
	/// An <see cref="Action"/> used to further configure the <see cref="MeterProviderBuilder"/>.
	/// This action is invoked after <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/setup/edot-defaults">EDOT .NET defaults</see>
	/// have been applied, but before the OTLP exporter is added. This ensures that any custom processors run before the exporter.
	/// </param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configure"/> is null.</exception>
	/// <returns>
	/// <para>
	///   An instance of <see cref="IOpenTelemetryBuilder"/> that can be used to further configure the
	///   OpenTelemetry SDK.
	/// </para>
	/// <para>
	///   <b>Warning:</b> Any further configuration applied via the returned <see cref="IOpenTelemetryBuilder"/> by calling <c>WithMetrics</c> will
	///   be applied <em>after</em> the OTLP exporter has been added to the <see cref="MeterProviderBuilder"/>. Use the
	///   <paramref name="configure"/> parameter to apply customisations before the exporter is added.
	/// </para>
	/// </returns>
	public static IOpenTelemetryBuilder WithElasticMetrics(this IOpenTelemetryBuilder builder, Action<MeterProviderBuilder> configure)
	{
		// We don't capture the stack trace here as we'll have that logged deeper in the call stack if needed.
		if (BootstrapLogger.IsEnabled)
			BootstrapLogger.Log($"{WithElasticMetricsMethodWithConfigureAction} invoked on builder with object hash '{RuntimeHelpers.GetHashCode(builder)}'.");

		// TODO - Breaking change: In a future major release, rename this parameter to 'configureBuilder' for clarity and consistency.
		// This would be a source breaking change only but we'll reserve it for a major version to avoid disrupting consumers.

#if NET
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (configure is null)
			throw new ArgumentNullException(nameof(configure));
#endif
		var builderOptions = new BuilderOptions<MeterProviderBuilder>
		{
			UserProvidedConfigureBuilder = configure,
			CalleeName = WithElasticMetricsMethodWithConfigureAction
		};

		return builder.WithMetrics(mpb => mpb.WithElasticDefaultsCore(null, null, builder.Services, builderOptions));
	}

	/// <summary>
	/// Adds metrics services into the <see cref="IOpenTelemetryBuilder"/> using Elastic Distribution of
	/// OpenTelemetry (EDOT) .NET <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/setup/edot-defaults">defaults</see>.
	/// </summary>
	/// <remarks>
	/// <para>
	///   <b>Guidance:</b> Prefer using the <c>AddElasticOpenTelemetry</c> method on the <see cref="IHostApplicationBuilder"/>
	///   or <see cref="IServiceCollection"/> rather than calling this method on the <see cref="IOpenTelemetryBuilder"/> directly.
	/// </para>
	/// <para>
	///   The <c>WithElasticMetrics</c> methods are primarily intended for advanced scenarios, non-host-based applications or when developing
	///   libraries that need to customize the OpenTelemetry configuration.
	/// </para>
	/// <para>
	///   <b>Recommendation:</b>When not using <c>AddElasticOpenTelemetry</c>, it is strongly recommended to use the
	///   <see cref="WithElasticMetrics(IOpenTelemetryBuilder, IConfiguration, Action{MeterProviderBuilder})"/> overload instead. This accepts
	///   an <see cref="Action{MeterProviderBuilder}"/> configuration callback and ensures any customisations are applied before
	///   the OTLP exporter is added.
	/// </para>
	/// </remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(IOpenTelemetryBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configuration"><inheritdoc cref="WithElasticDefaults(IOpenTelemetryBuilder, IConfiguration)" path="/param[@name='configuration']"/></param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configuration"/> is null.</exception>
	/// <returns>
	/// <para>
	///   An instance of <see cref="IOpenTelemetryBuilder"/> that can be used to further configure the
	///   OpenTelemetry SDK.
	/// </para>
	/// <para>
	///   <b>Warning:</b> Any further configuration applied via the returned <see cref="IOpenTelemetryBuilder"/> by calling <c>WithMetrics</c> will
	///   be applied <em>after</em> the OTLP exporter has been added to the <see cref="MeterProviderBuilder"/>.
	///   </para>
	/// <para>
	///   <b>Recommendation:</b>It is strongly recommended to use the <see cref="WithElasticMetrics(IOpenTelemetryBuilder, IConfiguration, Action{MeterProviderBuilder})"/> overload
	///   to customise the builder instead. This accepts an <see cref="Action{MeterProviderBuilder}"/> configuration callback and ensures any
	///   customisations are applied before the OTLP exporter is added.
	/// </para>
	/// <para>
	///   Alternatively, the <see cref="ElasticOpenTelemetryOptions.SkipOtlpExporter"/> <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/configuration#skipotlpexporter">option</see>
	///   can be used to prevent automatic addition of the OTLP exporter, allowing you to add it manually at a later stage.
	/// </para>
	/// </returns>
	public static IOpenTelemetryBuilder WithElasticMetrics(this IOpenTelemetryBuilder builder, IConfiguration configuration)
	{
		// We don't capture the stack trace here as we'll have that logged deeper in the call stack if needed.
		if (BootstrapLogger.IsEnabled)
			BootstrapLogger.Log($"{WithElasticMetricsMethodWithIConfiguration} invoked on builder with object hash '{RuntimeHelpers.GetHashCode(builder)}'.");

#if NET
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (configuration is null)
			throw new ArgumentNullException(nameof(configuration));
#endif
		var builderOptions = new BuilderOptions<MeterProviderBuilder>
		{
			CalleeName = WithElasticMetricsMethodWithIConfiguration
		};

		return builder.WithMetrics(mpb => mpb.WithElasticDefaultsCore(new(configuration), null, builder.Services, builderOptions));
	}

	/// <summary><inheritdoc cref="WithElasticMetrics(IOpenTelemetryBuilder,IConfiguration)" /></summary>
	/// <remarks><inheritdoc cref="WithElasticMetrics(IOpenTelemetryBuilder, IConfiguration)" path="/remarks"/></remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(IOpenTelemetryBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configuration"><inheritdoc cref="WithElasticDefaults(IOpenTelemetryBuilder, IConfiguration)" path="/param[@name='configuration']"/></param>
	/// <param name="configure"><inheritdoc cref="WithElasticMetrics(IOpenTelemetryBuilder, Action{MeterProviderBuilder})" path="/param[@name='configure']"/></param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configuration"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configure"/> is null.</exception>
	/// <returns><inheritdoc cref="WithElasticMetrics(IOpenTelemetryBuilder, Action{MeterProviderBuilder})" path="/returns"/></returns>
	public static IOpenTelemetryBuilder WithElasticMetrics(this IOpenTelemetryBuilder builder, IConfiguration configuration,
		Action<MeterProviderBuilder> configure)
	{
		// TODO - Breaking change: In a future major release, rename this parameter to 'configureBuilder' for clarity and consistency.
		// This would be a source breaking change only but we'll reserve it for a major version to avoid disrupting consumers.

		// We don't capture the stack trace here as we'll have that logged deeper in the call stack if needed.
		if (BootstrapLogger.IsEnabled)
			BootstrapLogger.Log($"{WithElasticMetricsMethodWithIConfigurationAndConfigureAction} invoked on builder with object hash '{RuntimeHelpers.GetHashCode(builder)}'.");

#if NET
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentNullException.ThrowIfNull(configure);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (configuration is null)
			throw new ArgumentNullException(nameof(configuration));

		if (configure is null)
			throw new ArgumentNullException(nameof(configure));
#endif
		var builderOptions = new BuilderOptions<MeterProviderBuilder>
		{
			UserProvidedConfigureBuilder = configure,
			CalleeName = WithElasticMetricsMethodWithIConfigurationAndConfigureAction
		};

		return builder.WithMetrics(mpb => mpb.WithElasticDefaultsCore(new(configuration), null, builder.Services, builderOptions));
	}

	/// <summary>
	/// Adds tracing services into the <see cref="IOpenTelemetryBuilder"/> using Elastic Distribution of
	/// OpenTelemetry (EDOT) .NET <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/setup/edot-defaults">defaults</see>.
	/// </summary>
	/// <remarks>
	/// <para>
	///   <b>Guidance:</b> Prefer using the <c>AddElasticOpenTelemetry</c> method on the <see cref="IHostApplicationBuilder"/>
	///   or <see cref="IServiceCollection"/> rather than calling this method on the <see cref="IOpenTelemetryBuilder"/> directly.
	/// </para>
	/// <para>
	///   The <c>WithElasticTracing</c> methods are primarily intended for advanced scenarios, non-host-based applications or when developing
	///   libraries that need to customize the OpenTelemetry configuration.
	/// </para>
	/// <para>
	///   <b>Recommendation:</b>When not using <c>AddElasticOpenTelemetry</c>, it is strongly recommended to use the
	///   <see cref="WithElasticTracing(IOpenTelemetryBuilder, Action{TracerProviderBuilder})"/> overload instead. This accepts
	///   an <see cref="Action{TracerProviderBuilder}"/> configuration callback and ensures any customisations are applied before
	///   the OTLP exporter is added.
	/// </para>
	/// </remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(IOpenTelemetryBuilder)" path="/param[@name='builder']"/></param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <returns>
	/// <para>
	///   An instance of <see cref="IOpenTelemetryBuilder"/> that can be used to further configure the OpenTelemetry SDK.
	/// </para>
	/// <para>
	///   <b>Warning:</b> Any further configuration applied via the returned <see cref="IOpenTelemetryBuilder"/> by calling <c>WithTracing</c> will
	///   be applied <em>after</em> the OTLP exporter has been added to the <see cref="TracerProviderBuilder"/>.
	///   </para>
	/// <para>
	///   <b>Recommendation:</b>It is strongly recommended to use the <see cref="WithElasticTracing(IOpenTelemetryBuilder, Action{TracerProviderBuilder})"/> overload
	///   to customise the builder instead. This accepts an <see cref="Action{TracerProviderBuilder}"/> configuration callback and ensures any
	///   customisations are applied before the OTLP exporter is added.
	/// </para>
	/// <para>
	///   Alternatively, the <see cref="ElasticOpenTelemetryOptions.SkipOtlpExporter"/> <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/configuration#skipotlpexporter">option</see>
	///   can be used to prevent automatic addition of the OTLP exporter, allowing you to add it manually at a later stage.
	/// </para>
	/// </returns>
	public static IOpenTelemetryBuilder WithElasticTracing(this IOpenTelemetryBuilder builder)
	{
		// We don't capture the stack trace here as we'll have that logged deeper in the call stack if needed.
		if (BootstrapLogger.IsEnabled)
			BootstrapLogger.Log($"{WithElasticTracingMethodNoArgs} invoked on builder with object hash '{RuntimeHelpers.GetHashCode(builder)}'.");

#if NET
		ArgumentNullException.ThrowIfNull(builder);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));
#endif
		var builderOptions = new BuilderOptions<TracerProviderBuilder>
		{
			CalleeName = WithElasticTracingMethodNoArgs
		};

		return builder.WithTracing(m => m.WithElasticDefaultsCore(null, null, builder.Services, builderOptions));
	}

	/// <summary><inheritdoc cref="WithElasticTracing(IOpenTelemetryBuilder)" /></summary>
	/// <remarks>
	/// <para>
	///   <b>Guidance:</b> Prefer using the <c>AddElasticOpenTelemetry</c> method on the <see cref="IHostApplicationBuilder"/>
	///   or <see cref="IServiceCollection"/> rather than calling this method on the <see cref="IOpenTelemetryBuilder"/> directly.
	/// </para>
	/// <para>
	///   The <c>WithElasticTracing</c> methods are primarily intended for advanced scenarios, non-host-based applications or when developing
	///   libraries that need to customize the OpenTelemetry configuration.
	/// </para>
	/// </remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticTracing(IOpenTelemetryBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configure">
	/// An <see cref="Action"/> used to further configure the <see cref="TracerProviderBuilder"/>.
	/// This action is invoked after <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/setup/edot-defaults">EDOT .NET defaults</see>
	/// have been applied, but before the OTLP exporter is added. This ensures that any custom processors run before the exporter.
	/// </param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configure"/> is null.</exception>
	/// <returns>
	/// <para>
	///   An instance of <see cref="IOpenTelemetryBuilder"/> that can be used to further configure the
	///   OpenTelemetry SDK.
	/// </para>
	/// <para>
	///   <b>Warning:</b> Any further configuration applied via the returned <see cref="IOpenTelemetryBuilder"/> by calling <c>WithTracing</c> will
	///   be applied <em>after</em> the OTLP exporter has been added to the <see cref="TracerProviderBuilder"/>. Use the
	///   <paramref name="configure"/> parameter to apply customisations before the exporter is added.
	/// </para>
	/// </returns>
	public static IOpenTelemetryBuilder WithElasticTracing(this IOpenTelemetryBuilder builder, Action<TracerProviderBuilder> configure)
	{
		// TODO - Breaking change: In a future major release, rename this parameter to 'configureBuilder' for clarity and consistency.
		// This would be a source breaking change only but we'll reserve it for a major version to avoid disrupting consumers.

		// We don't capture the stack trace here as we'll have that logged deeper in the call stack if needed.
		if (BootstrapLogger.IsEnabled)
			BootstrapLogger.Log($"{WithElasticTracingMethodWithConfigureAction} invoked on builder with object hash '{RuntimeHelpers.GetHashCode(builder)}'.");

#if NET
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (configure is null)
			throw new ArgumentNullException(nameof(configure));
#endif

		var builderOptions = new BuilderOptions<TracerProviderBuilder>
		{
			UserProvidedConfigureBuilder = configure,
			CalleeName = WithElasticTracingMethodWithConfigureAction
		};

		return builder.WithTracing(tpb => tpb.WithElasticDefaultsCore(null, null, builder.Services, builderOptions));
	}

	/// <summary>
	/// Adds tracing services into the <see cref="IOpenTelemetryBuilder"/> using Elastic Distribution of
	/// OpenTelemetry (EDOT) .NET <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/setup/edot-defaults">defaults</see>.
	/// </summary>
	/// <remarks>
	/// <para>
	///   <b>Guidance:</b> Prefer using the <c>AddElasticOpenTelemetry</c> method on the <see cref="IHostApplicationBuilder"/>
	///   or <see cref="IServiceCollection"/> rather than calling this method on the <see cref="IOpenTelemetryBuilder"/> directly.
	/// </para>
	/// <para>
	///   The <c>WithElasticTracing</c> methods are primarily intended for advanced scenarios, non-host-based applications or when developing
	///   libraries that need to customize the OpenTelemetry configuration.
	/// </para>
	/// <para>
	///   <b>Recommendation:</b>When not using <c>AddElasticOpenTelemetry</c>, it is strongly recommended to use the
	///   <see cref="WithElasticTracing(IOpenTelemetryBuilder, IConfiguration, Action{TracerProviderBuilder})"/> overload instead. This accepts
	///   an <see cref="Action{TracerProviderBuilder}"/> configuration callback and ensures any customisations are applied before
	///   the OTLP exporter is added.
	/// </para>
	/// </remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticTracing(IOpenTelemetryBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configuration"><inheritdoc cref="WithElasticTracing(IOpenTelemetryBuilder, IConfiguration)" path="/param[@name='configuration']"/></param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configuration"/> is null.</exception>
	/// <returns>
	/// <para>
	///   An instance of <see cref="IOpenTelemetryBuilder"/> that can be used to further configure the
	///   OpenTelemetry SDK.
	/// </para>
	/// <para>
	///   <b>Warning:</b> Any further configuration applied via the returned <see cref="IOpenTelemetryBuilder"/> by calling <c>WithTracing</c> will
	///   be applied <em>after</em> the OTLP exporter has been added to the <see cref="TracerProviderBuilder"/>.
	///   </para>
	/// <para>
	///   <b>Recommendation:</b>It is strongly recommended to use the <see cref="WithElasticTracing(IOpenTelemetryBuilder, IConfiguration, Action{TracerProviderBuilder})"/> overload
	///   to customise the builder instead. This accepts an <see cref="Action{TracerProviderBuilder}"/> configuration callback and ensures any
	///   customisations are applied before the OTLP exporter is added.
	/// </para>
	/// <para>
	///   Alternatively, the <see cref="ElasticOpenTelemetryOptions.SkipOtlpExporter"/> <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/configuration#skipotlpexporter">option</see>
	///   can be used to prevent automatic addition of the OTLP exporter, allowing you to add it manually at a later stage.
	/// </para>
	/// </returns>
	public static IOpenTelemetryBuilder WithElasticTracing(this IOpenTelemetryBuilder builder, IConfiguration configuration)
	{
		// We don't capture the stack trace here as we'll have that logged deeper in the call stack if needed.
		if (BootstrapLogger.IsEnabled)
			BootstrapLogger.Log($"{WithElasticTracingMethodWithIConfiguration} invoked on builder with object hash '{RuntimeHelpers.GetHashCode(builder)}'.");

#if NET
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (configuration is null)
			throw new ArgumentNullException(nameof(configuration));
#endif
		var builderOptions = new BuilderOptions<TracerProviderBuilder>
		{
			CalleeName = WithElasticTracingMethodWithIConfiguration
		};

		return builder.WithTracing(tpb => tpb.WithElasticDefaultsCore(new(configuration), null, builder.Services, builderOptions));
	}

	/// <summary><inheritdoc cref="WithElasticTracing(IOpenTelemetryBuilder,IConfiguration)" /></summary>
	/// <remarks><inheritdoc cref="WithElasticTracing(IOpenTelemetryBuilder, IConfiguration)" path="/remarks"/></remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(IOpenTelemetryBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configuration"><inheritdoc cref="WithElasticDefaults(IOpenTelemetryBuilder, IConfiguration)" path="/param[@name='configuration']"/></param>
	/// <param name="configure"><inheritdoc cref="WithElasticTracing(IOpenTelemetryBuilder, Action{TracerProviderBuilder})" path="/param[@name='configure']"/></param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configuration"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configure"/> is null.</exception>
	/// <returns><inheritdoc cref="WithElasticTracing(IOpenTelemetryBuilder, Action{TracerProviderBuilder})" path="/returns"/></returns>
	public static IOpenTelemetryBuilder WithElasticTracing(this IOpenTelemetryBuilder builder, IConfiguration configuration,
		Action<TracerProviderBuilder> configure)
	{
		// TODO - Breaking change: In a future major release, rename this parameter to 'configureBuilder' for clarity and consistency.
		// This would be a source breaking change only but we'll reserve it for a major version to avoid disrupting consumers.

		// We don't capture the stack trace here as we'll have that logged deeper in the call stack if needed.
		if (BootstrapLogger.IsEnabled)
			BootstrapLogger.Log($"{WithElasticTracingMethodWithIConfigurationAndConfigureAction} invoked on builder with object hash '{RuntimeHelpers.GetHashCode(builder)}'.");

#if NET
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentNullException.ThrowIfNull(configure);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (configuration is null)
			throw new ArgumentNullException(nameof(configuration));

		if (configure is null)
			throw new ArgumentNullException(nameof(configure));
#endif
		var builderOptions = new BuilderOptions<TracerProviderBuilder>
		{
			UserProvidedConfigureBuilder = configure,
			CalleeName = WithElasticTracingMethodWithIConfigurationAndConfigureAction
		};

		return builder.WithTracing(tpb => tpb.WithElasticDefaultsCore(new(configuration), null, builder.Services, builderOptions));
	}

	/// <summary>
	/// Internal core implementation for the various overloads of <c>WithElasticDefaults</c>.
	/// May also be called by other internal methods that need to apply EDOT .NET defaults.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static IOpenTelemetryBuilder WithElasticDefaultsCore(
		this IOpenTelemetryBuilder builder,
		CompositeElasticOpenTelemetryOptions options,
		in BuilderOptions<IOpenTelemetryBuilder> builderOptions)
	{
		var callCount = Interlocked.Increment(ref WithElasticDefaultsCallCount);

		// We don't capture the stack trace here as we'll have that logged deeper in the call stack if needed.
		if (BootstrapLogger.IsEnabled)
		{
			BootstrapLogger.Log($"{nameof(OpenTelemetryBuilderExtensions)}.{nameof(WithElasticDefaultsCore)} invoked " +
				$"on builder with object hash '{RuntimeHelpers.GetHashCode(builder)}'. Invokation count: {callCount}." +
				$"{Environment.NewLine}    Invoked with `{nameof(CompositeElasticOpenTelemetryOptions)}` instance '{options.InstanceId}'.");
			BootstrapLogger.LogBuilderOptions(builderOptions, nameof(OpenTelemetryBuilderExtensions), nameof(WithElasticDefaultsCore));
		}

		// FullName may return null so we fallback to Name when required.
		var providerBuilderName = builder.GetType().FullName ?? builder.GetType().Name;

		var logger = SignalBuilder.GetLogger(builder, null, options, null);
		logger.LogCallerInfo(builderOptions);

		if (callCount > 1)
		{
			logger.LogMultipleWithElasticDefaultsCallsWarning(callCount, providerBuilderName);
		}

		// If for some reason `WithElasticDefaults` is invoked with the `Signals` option set to
		// none, we skip bootstrapping entirely. We log this as a warning since it's best to
		// simply not call `WithElasticDefaults` in this scenario and it may indicate a misconfiguration.
		if (options.Signals == Signals.None)
		{
			logger.LogSkippingBootstrapWarning();
			return builder;
		}

		SignalBuilder.WithElasticDefaults(builder, options, null, builder.Services, builderOptions, ConfigureBuilder);

		return builder;
	}

	private static void ConfigureBuilder(BuilderContext<IOpenTelemetryBuilder> builderContext)
	{
		var builder = builderContext.Builder;
		var components = builderContext.BuilderState.Components;
		var options = builderContext.BuilderState.Components.Options;
		var builderState = builderContext.BuilderState;
		var services = builderContext.Services;

		// Configure tracing, if the signal is enabled.
		if (options.Signals.HasFlagFast(Signals.Traces))
		{
			// When the user has provided their own configuration callback for the IOpenTelemetryBuilder
			// we don't want WithElasticDefaults for the TracerProviderBuilder to add the OTLP exporter so
			// we defer it until after this method runs the user-provided callback.
			var builderOptions = new BuilderOptions<TracerProviderBuilder>
			{
				DeferAddOtlpExporter = builderContext.BuilderOptions.UserProvidedConfigureBuilder is not null,
				SkipLogCallerInfo = builderContext.BuilderOptions.SkipLogCallerInfo
			};

			builder.WithTracing(tpb => tpb.WithElasticDefaultsCore(components.Options, components, services, builderOptions));
		}
		else
		{
			components.Logger.LogSignalDisabled(Signals.Traces.ToString().ToLower(),
				nameof(IOpenTelemetryBuilder), builderState.InstanceIdentifier);
		}

		if (options.Signals.HasFlagFast(Signals.Metrics))
		{
			// When the user has provided their own configuration callback for the IOpenTelemetryBuilder
			// we don't want WithElasticDefaults for the MeterProviderBuilder to add the OTLP exporter so
			// we defer it until after this method runs the user-provided callback.
			var builderOptions = new BuilderOptions<MeterProviderBuilder>
			{
				DeferAddOtlpExporter = builderContext.BuilderOptions.UserProvidedConfigureBuilder is not null,
				SkipLogCallerInfo = builderContext.BuilderOptions.SkipLogCallerInfo
			};

			builder.WithMetrics(mpb => mpb.WithElasticDefaultsCore(components.Options, components, builder.Services, builderOptions));
		}
		else
		{
			components.Logger.LogSignalDisabled(Signals.Metrics.ToString().ToLower(),
				nameof(IOpenTelemetryBuilder), builderState.InstanceIdentifier);
		}

		if (options.Signals.HasFlagFast(Signals.Logs))
		{
			// When the user has provided their own configuration callback for the IOpenTelemetryBuilder
			// we don't want WithElasticDefaults for the MeterProviderBuilder to add the OTLP exporter so
			// we defer it until after this method runs the user-provided callback.
			var builderOptions = new BuilderOptions<LoggerProviderBuilder>
			{
				DeferAddOtlpExporter = builderContext.BuilderOptions.UserProvidedConfigureBuilder is not null,
				SkipLogCallerInfo = builderContext.BuilderOptions.SkipLogCallerInfo
			};

			builder.WithLogging(lpb => lpb.WithElasticDefaultsCore(components.Options, components, builder.Services, builderOptions));
		}
		else
		{
			components.Logger.LogSignalDisabled(Signals.Logs.ToString().ToLower(),
				nameof(IOpenTelemetryBuilder), builderState.InstanceIdentifier);
		}

		// FullName may return null so we fallback to Name when required.
		var builderTypeName = builder.GetType().FullName ?? builder.GetType().Name;

		if (builderContext.BuilderOptions.UserProvidedConfigureBuilder is not null)
		{
			// Run the user-provided builder configuration after the EDOT defaults have been applied.
			builderContext.BuilderOptions.UserProvidedConfigureBuilder(builder);
			components.Logger.LogInvokedConfigureAction(builderTypeName, builderState.InstanceIdentifier);

			// Add OTLP exporters (if enabled) after user-provided configuration
			HandleOtlpExporter(Signals.Traces, b => b.WithTracing(tpb => tpb.AddOtlpExporter()));
			HandleOtlpExporter(Signals.Metrics, b => b.WithMetrics(tpb => tpb.AddOtlpExporter()));
			HandleOtlpExporter(Signals.Logs, b => b.WithLogging(tpb => tpb.AddOtlpExporter()));
		}

		void HandleOtlpExporter(Signals signal, Action<IOpenTelemetryBuilder> configure)
		{
			// If the signal is enabled and the user provided their own configuration callback,
			// we need to ensure the OTLP exporter is added after running the user-provided callback.
			if (options.Signals.HasFlagFast(signal))
			{
				if (builderState.Components.Options.SkipOtlpExporter)
				{
					components.Logger.LogSkippedOtlpExporter(signal.ToString(), builderTypeName, builderState.InstanceIdentifier);
				}
				else
				{
					configure(builder);
					components.Logger.LogAddedOtlpExporter(signal.ToString(), builderTypeName, builderState.InstanceIdentifier);
				}
			}

			// NOTE: We don't log signal disabled here since that will already have been logged above.
		}
	}
}
