// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.Hosting;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Elastic extension methods for <see cref="IHostApplicationBuilder"/> used to register
/// the Elastic Distribution of OpenTelemetry (EDOT) .NET defaults.
/// </summary>
public static class HostApplicationBuilderExtensions
{
	/// <summary>
	/// Registers the OpenTelemetry SDK with the application, configured with Elastic Distribution of OpenTelemetry (EDOT) .NET defaults.
	/// </summary>
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
	/// Configuration is first bound from <see cref="IConfiguration"/> and then overridden by any options configured on
	/// the provided <paramref name="options"/>.
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
		ArgumentNullException.ThrowIfNull(builder);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (options is null)
			throw new ArgumentNullException(nameof(options));
#endif

		builder.Services.AddElasticOpenTelemetry(builder.Configuration, options);

		return builder;
	}

	/// <summary>
	/// <inheritdoc cref="AddElasticOpenTelemetry(IHostApplicationBuilder)" />
	/// </summary>
	/// <param name="builder"><inheritdoc cref="AddElasticOpenTelemetry(IHostApplicationBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="options"><see cref="ElasticOpenTelemetryOptions"/> used to configure the Elastic Distribution of OpenTelemetry (EDOT) .NET.</param>
	/// <param name="configure"><see cref="IOpenTelemetryBuilder"/> configuration action.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="options"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configure"/> is null.</exception>
	/// <returns><inheritdoc cref="AddElasticOpenTelemetry(IHostApplicationBuilder)" /></returns>
	public static IHostApplicationBuilder AddElasticOpenTelemetry(this IHostApplicationBuilder builder, ElasticOpenTelemetryOptions options, Action<IOpenTelemetryBuilder> configure)
	{
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

		var otelBuilder = builder.Services
			.AddElasticOpenTelemetry(builder.Configuration, options);

		configure.Invoke(otelBuilder);

		return builder;
	}

	/// <summary>
	/// <inheritdoc cref="AddElasticOpenTelemetry(IHostApplicationBuilder)" />
	/// </summary>
	/// <param name="builder"><inheritdoc cref="AddElasticOpenTelemetry(IHostApplicationBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configure"><see cref="IOpenTelemetryBuilder"/> configuration action.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configure"/> is null.</exception>
	/// <returns><inheritdoc cref="AddElasticOpenTelemetry(IHostApplicationBuilder)" /></returns>
	public static IHostApplicationBuilder AddElasticOpenTelemetry(this IHostApplicationBuilder builder, Action<IOpenTelemetryBuilder> configure)
	{
#if NET
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (configure is null)
			throw new ArgumentNullException(nameof(configure));
#endif

		var otelBuilder = builder.Services
			.AddElasticOpenTelemetry(builder.Configuration);

		configure.Invoke(otelBuilder);

		return builder;
	}
}
