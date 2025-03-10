// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Runtime.CompilerServices;
using Elastic.OpenTelemetry;
using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;

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
	/// Registers the OpenTelemetry SDK services with the provided <see cref="IServiceCollection"/> to include the
	/// OpenTelemetry SDK in the application, configured with Elastic Distribution of OpenTelemetry (EDOT) .NET defaults.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection"/> for adding services.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="services"/> is null.</exception>
	/// <returns>
	/// An instance of <see cref="IOpenTelemetryBuilder"/> that can be used to further configure the
	/// OpenTelemetry SDK.
	/// </returns>
	public static IOpenTelemetryBuilder AddElasticOpenTelemetry(this IServiceCollection services)
	{
#if NET
		ArgumentNullException.ThrowIfNull(services);
#else
		if (services is null)
			throw new ArgumentNullException(nameof(services));
#endif

		return AddElasticOpenTelemetryCore(services, CompositeElasticOpenTelemetryOptions.DefaultOptions);
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
	/// <returns><inheritdoc cref="AddElasticOpenTelemetry(IServiceCollection)"/></returns>
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

		return AddElasticOpenTelemetryCore(services, new(options));
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
	/// <returns><inheritdoc cref="AddElasticOpenTelemetry(IServiceCollection)"/></returns>
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

		return AddElasticOpenTelemetryCore(services, new(configuration));
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
	internal static IOpenTelemetryBuilder AddElasticOpenTelemetry(this IServiceCollection services, IConfiguration configuration, ElasticOpenTelemetryOptions options)
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

		return AddElasticOpenTelemetryCore(services, new(configuration, options));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static IOpenTelemetryBuilder AddElasticOpenTelemetryCore(IServiceCollection services, CompositeElasticOpenTelemetryOptions options)
	{
		var builder = services.AddOpenTelemetry().WithElasticDefaultsCore(options);

		if (!services.Any((ServiceDescriptor d) => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(ElasticOpenTelemetryService)))
		{
			services.Insert(0, ServiceDescriptor.Singleton<IHostedService, ElasticOpenTelemetryService>());
		}

		return builder;
	}
}
