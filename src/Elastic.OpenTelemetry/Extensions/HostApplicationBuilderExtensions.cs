// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Resources;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.Hosting;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Elastic extension methods for <see cref="IHostApplicationBuilder"/> used to register
/// the Elastic Distribution of OpenTelemetry (EDOT) defaults.
/// </summary>
public static class HostApplicationBuilderExtensions
{
	/// <summary>
	/// Registers the OpenTelemetry SDK with the application, configured with Elastic Distribution of OpenTelemetry (EDOT) defaults.
	/// </summary>
	/// <param name="builder">The <see cref="IHostApplicationBuilder"/> for the application being configured.</param>
	/// <returns>The supplied <see cref="IHostApplicationBuilder"/> for chaining calls.</returns>
	public static IHostApplicationBuilder AddElasticOpenTelemetry(this IHostApplicationBuilder builder)
	{
		builder.Services.AddElasticOpenTelemetry(builder.Configuration);
		return builder;
	}

	/// <summary>
	/// <inheritdoc cref="AddElasticOpenTelemetry(IHostApplicationBuilder)" />
	/// </summary>
	/// <param name="builder"><inheritdoc cref="AddElasticOpenTelemetry(IHostApplicationBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="serviceName">A <see cref="string"/> representing the logical name of the service sent with resource attributes.</param>
	/// <returns><inheritdoc cref="AddElasticOpenTelemetry(IHostApplicationBuilder)" /></returns>
	public static IHostApplicationBuilder AddElasticOpenTelemetry(this IHostApplicationBuilder builder, string serviceName)
	{
#if NET
		ArgumentException.ThrowIfNullOrEmpty(serviceName);
#else
		if (string.IsNullOrEmpty(serviceName))
			throw new ArgumentNullException(nameof(serviceName));
#endif

		return AddElasticOpenTelemetry(builder, r => r.AddService(serviceName));
	}

	/// <summary>
	/// <inheritdoc cref="AddElasticOpenTelemetry(IHostApplicationBuilder)" />
	/// </summary>
	/// <param name="builder"><inheritdoc cref="AddElasticOpenTelemetry(IHostApplicationBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configureResource"><see cref="ResourceBuilder"/> configuration action.</param>
	/// <returns><inheritdoc cref="AddElasticOpenTelemetry(IHostApplicationBuilder)" /></returns>
	public static IHostApplicationBuilder AddElasticOpenTelemetry(this IHostApplicationBuilder builder, Action<ResourceBuilder> configureResource)
	{
#if NET
		ArgumentNullException.ThrowIfNull(configureResource);
#else
		if (configureResource is null)
			throw new ArgumentNullException(nameof(configureResource));
#endif

		builder.Services
			.AddElasticOpenTelemetry(builder.Configuration)
			.ConfigureResource(configureResource);

		return builder;
	}
}
