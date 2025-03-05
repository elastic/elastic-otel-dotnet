// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Resources;

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
	/// <param name="builder"><inheritdoc cref="AddElasticOpenTelemetry(IHostApplicationBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="serviceName">A <see cref="string"/> representing the logical name of the service sent with resource attributes.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentException">Thrown when the <paramref name="serviceName"/> is null or empty.</exception>
	/// <returns><inheritdoc cref="AddElasticOpenTelemetry(IHostApplicationBuilder)" /></returns>
	public static IHostApplicationBuilder AddElasticOpenTelemetry(this IHostApplicationBuilder builder, string serviceName)
	{
#if NET
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrEmpty(serviceName);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (string.IsNullOrEmpty(serviceName))
			throw new ArgumentException(nameof(serviceName));
#endif

		return AddElasticOpenTelemetry(builder, r => r.ConfigureResource(r => r.AddService(serviceName)));
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
