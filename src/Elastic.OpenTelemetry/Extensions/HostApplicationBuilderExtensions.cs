// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry;
using Elastic.OpenTelemetry.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.Hosting;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Elastic extension methods for <see cref="IHostApplicationBuilder"/> used to register
/// the Elastic Distribution of OpenTelemetry (EDOT) .NET <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/setup/edot-defaults">defaults</see>.
/// </summary>
public static class HostApplicationBuilderExtensions
{
	/// <summary>
	/// Registers the OpenTelemetry SDK with the application, configured with Elastic Distribution of OpenTelemetry (EDOT) .NET
	/// <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/setup/edot-defaults">defaults</see> for traces,
	/// metrics and logs.
	/// </summary>
	/// <remarks>
	/// <para>
	///   For applications built on the Generic Host (i.e. using <c>Host.CreateDefaultBuilder</c>), this is the prefered way
	///   to register the OpenTelemetry SDK with EDOT .NET defaults. It enables EDOT .NET defaults for all signals, requiring
	///   minimal code.
	/// </para>
	/// <para>
	///   When using <c>AddElasticOpenTelemetry</c>, you may need to further customize the OpenTelemetry SDK configuration
	///   such as when you need to add additional sources, processors or instrumentations. You can do this by calling the overload
	///   accepting a configuration action <see cref="AddElasticOpenTelemetry(IHostApplicationBuilder, Action{IOpenTelemetryBuilder})"/>.
	/// </para>
	/// <para>
	///   Avoid combining this method with calls to <c>WithElasticDefaults</c>, <c>WithElasticMetrics</c>, <c>WithElasticLogs</c> or <c>WithElasticTraces</c>
	///   extension methods on the <see cref="IOpenTelemetryBuilder"/>, <see cref="TracerProviderBuilder"/>, <see cref="MeterProviderBuilder"/> or <see cref="LoggerProviderBuilder"/>
	///   directly as this may lead to unexpected results.
	/// </para>
	/// <para>
	///   Alternatively, the <see cref="ElasticOpenTelemetryOptions.SkipOtlpExporter"/> <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/configuration#skipotlpexporter">option</see>
	///   can be used to prevent automatic addition of the OTLP exporter, allowing you to add it manually at a later stage.
	/// </para>
	/// </remarks>
	/// <param name="builder">The <see cref="IHostApplicationBuilder"/> for the application being configured.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <returns>The supplied <see cref="IHostApplicationBuilder"/> for chaining calls.</returns>
	public static IHostApplicationBuilder AddElasticOpenTelemetry(this IHostApplicationBuilder builder)
	{
#if NET
        ArgumentNullException.ThrowIfNull(builder);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));
#endif

		builder.Services.AddElasticOpenTelemetry(builder.Configuration);

		return builder;
	}

	/// <summary>
	/// <inheritdoc cref="AddElasticOpenTelemetry(IHostApplicationBuilder)" />
	/// </summary>
	/// <remarks>
	/// <para>
	///   For applications built on the Generic Host (i.e. using <c>Host.CreateDefaultBuilder</c>), this is the prefered way
	///   to register the OpenTelemetry SDK with EDOT .NET defaults. It enables EDOT .NET defaults for all signals, requiring
	///   minimal code.
	/// </para>
	/// <para>
	///   Avoid combining this method with calls to <c>WithElasticDefaults</c>, <c>WithElasticMetrics</c>, <c>WithElasticLogs</c> or <c>WithElasticTraces</c>
	///   extension methods on the <see cref="IOpenTelemetryBuilder"/>, <see cref="TracerProviderBuilder"/>, <see cref="MeterProviderBuilder"/> or <see cref="LoggerProviderBuilder"/>
	///   directly as this may lead to unexpected results.
	/// </para>
	/// <para>
	///   Alternatively, the <see cref="ElasticOpenTelemetryOptions.SkipOtlpExporter"/> <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/configuration#skipotlpexporter">option</see>
	///   can be used to prevent automatic addition of the OTLP exporter, allowing you to add it manually at a later stage.
	/// </para>
	/// </remarks>
	/// <param name="builder"><inheritdoc cref="AddElasticOpenTelemetry(IHostApplicationBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configure">A <see cref="IOpenTelemetryBuilder"/> configuration <see cref="Action{T}" /> used to further customise
	/// the OpenTelemetry SDK after Elastic Distribution of OpenTelemetry (EDOT) .NET defaults have been applied and before the OTLP
	/// exporter is added.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configure"/> is null.</exception>
	/// <returns><inheritdoc cref="AddElasticOpenTelemetry(IHostApplicationBuilder)" /></returns>
	public static IHostApplicationBuilder AddElasticOpenTelemetry(this IHostApplicationBuilder builder, Action<IOpenTelemetryBuilder> configure)
	{
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

		var builderOptions = new BuilderOptions<IOpenTelemetryBuilder>
		{
			UserProvidedConfigureBuilder = configure,
			// We don't set defer as we expect the callee to handle executing the configure action at the correct time.
			DeferAddOtlpExporter = false
		};

		builder.Services.AddElasticOpenTelemetryCore(new(builder.Configuration), builderOptions);
		return builder;
	}

	/// <summary>
	/// <inheritdoc cref="AddElasticOpenTelemetry(IHostApplicationBuilder)" />
	/// </summary>
	/// <remarks>
	/// <para>
	///   Configuration is first bound from <see cref="IConfiguration"/> and then overridden by any options configured on
	///   the provided <paramref name="options"/>.
	/// </para>
	/// <para>
	///   For applications built on the Generic Host (i.e. using <c>Host.CreateDefaultBuilder</c>), this is the prefered way
	///   to register the OpenTelemetry SDK with EDOT .NET defaults. It enables EDOT .NET defaults for all signals, requiring
	///   minimal code.
	/// </para>
	/// <para>
	///   When using <c>AddElasticOpenTelemetry</c>, you may need to further customize the OpenTelemetry SDK configuration
	///   such as when you need to add additional sources, processors or instrumentations. You can do this by calling the overload
	///   accepting a configuration action <see cref="AddElasticOpenTelemetry(IHostApplicationBuilder, ElasticOpenTelemetryOptions, Action{IOpenTelemetryBuilder})"/>.
	/// </para>
	/// <para>
	///   Avoid combining this method with calls to <c>WithElasticDefaults</c>, <c>WithElasticMetrics</c>, <c>WithElasticLogs</c> or <c>WithElasticTraces</c>
	///   extension methods on the <see cref="IOpenTelemetryBuilder"/>, <see cref="TracerProviderBuilder"/>, <see cref="MeterProviderBuilder"/> or <see cref="LoggerProviderBuilder"/>
	///   directly as this may lead to unexpected results.
	/// </para>
	/// <para>
	///   Alternatively, the <see cref="ElasticOpenTelemetryOptions.SkipOtlpExporter"/> <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/configuration#skipotlpexporter">option</see>
	///   can be used to prevent automatic addition of the OTLP exporter, allowing you to add it manually at a later stage.
	/// </para>
	/// </remarks>
	/// <param name="builder"><inheritdoc cref="AddElasticOpenTelemetry(IHostApplicationBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="options"><see cref="ElasticOpenTelemetryOptions"/> used to configure the Elastic Distribution of OpenTelemetry (EDOT) .NET.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="options"/> is null.</exception>
	/// <returns><inheritdoc cref="AddElasticOpenTelemetry(IHostApplicationBuilder)" /></returns>
	public static IHostApplicationBuilder AddElasticOpenTelemetry(this IHostApplicationBuilder builder, ElasticOpenTelemetryOptions options)
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

		builder.Services.AddElasticOpenTelemetryCore(new (builder.Configuration, options), default);
		return builder;
	}

	/// <summary>
	/// <inheritdoc cref="AddElasticOpenTelemetry(IHostApplicationBuilder)" />
	/// </summary>
	/// <remarks>
	/// <para>
	///   For applications built on the Generic Host (i.e. using <c>Host.CreateDefaultBuilder</c>), this is the prefered way
	///   to register the OpenTelemetry SDK with EDOT .NET defaults. It enables EDOT .NET defaults for all signals, requiring
	///   minimal code.
	/// </para>
	/// <para>
	///   Avoid combining this method with calls to <c>WithElasticDefaults</c>, <c>WithElasticMetrics</c>, <c>WithElasticLogs</c> or <c>WithElasticTraces</c>
	///   extension methods on the <see cref="IOpenTelemetryBuilder"/>, <see cref="TracerProviderBuilder"/>, <see cref="MeterProviderBuilder"/> or <see cref="LoggerProviderBuilder"/>
	///   directly as this may lead to unexpected results.
	/// </para>
	/// <para>
	///   Alternatively, the <see cref="ElasticOpenTelemetryOptions.SkipOtlpExporter"/> <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/configuration#skipotlpexporter">option</see>
	///   can be used to prevent automatic addition of the OTLP exporter, allowing you to add it manually at a later stage.
	/// </para>
	/// </remarks>
	/// <param name="builder"><inheritdoc cref="AddElasticOpenTelemetry(IHostApplicationBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="options"><inheritdoc cref="AddElasticOpenTelemetry(IHostApplicationBuilder, ElasticOpenTelemetryOptions)" path="/param[@name='options']"/></param>
	/// <param name="configure"><inheritdoc cref="AddElasticOpenTelemetry(IHostApplicationBuilder, Action{IOpenTelemetryBuilder})" path="/param[@name='configure']"/></param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="options"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configure"/> is null.</exception>
	/// <returns><inheritdoc cref="AddElasticOpenTelemetry(IHostApplicationBuilder)" /></returns>
	public static IHostApplicationBuilder AddElasticOpenTelemetry(this IHostApplicationBuilder builder,
		ElasticOpenTelemetryOptions options, Action<IOpenTelemetryBuilder> configure)
	{
		// TODO - Breaking change: In a future major release, rename this parameter to 'configureBuilder' for clarity and consistency.
		// This would be a source breaking change only but we'll reserve it for a major version to avoid disrupting consumers.
#if NET
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(configure);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (options is null)
			throw new ArgumentNullException(nameof(options));

		if (configure is null)
			throw new ArgumentNullException(nameof(configure));
#endif
		var builderOptions = new BuilderOptions<IOpenTelemetryBuilder>
		{
			UserProvidedConfigureBuilder = configure,
			// We don't set defer as we expect the callee to handle executing the configure action at the correct time.
			DeferAddOtlpExporter = false
		};

		builder.Services.AddElasticOpenTelemetryCore(new (builder.Configuration, options), builderOptions);
		return builder;
	}
}
