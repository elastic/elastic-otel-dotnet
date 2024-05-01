// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry;
using Elastic.OpenTelemetry.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenTelemetry;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers the Elastic OpenTelemetry builder with the provided <see cref="IServiceCollection"/>.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection"/> for adding services.</param>
	/// <returns>
	/// An instance of <see cref="IOpenTelemetryBuilder"/> that can be used to further configure the
	/// OpenTelemetry SDK.
	/// </returns>
	public static IOpenTelemetryBuilder AddElasticOpenTelemetry(this IServiceCollection services) =>
		services.AddElasticOpenTelemetry(new ElasticOpenTelemetryBuilderOptions { Services = services });

	/// <summary>
	/// Registers the Elastic OpenTelemetry builder with the provided <see cref="IServiceCollection"/>.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection"/> for adding services.</param>
	/// <param name="configuration">
	/// An <see cref="IConfiguration"/> instance from which to attempt binding of configuration values.
	/// </param>
	/// <returns>
	/// An instance of <see cref="IOpenTelemetryBuilder"/> that can be used to further configure the
	/// OpenTelemetry SDK.
	/// </returns>
	public static IOpenTelemetryBuilder AddElasticOpenTelemetry(this IServiceCollection services, IConfiguration configuration) =>
		services.AddElasticOpenTelemetry(new ElasticOpenTelemetryBuilderOptions
		{
			Services = services,
			DistroOptions = new ElasticOpenTelemetryOptions(configuration)
		});

	/// <summary>
	/// Registers the Elastic OpenTelemetry builder with the provided <see cref="IServiceCollection"/>.
	/// </summary>	
	/// <param name="services">The <see cref="IServiceCollection"/> for adding services.</param>
	/// <param name="options"><see cref="ElasticOpenTelemetryBuilderOptions"/> for the initial OpenTelemetry registration.</param>
	/// <returns>
	/// An instance of <see cref="IOpenTelemetryBuilder"/> that can be used to further configure the
	/// OpenTelemetry SDK.
	/// </returns>
	public static IOpenTelemetryBuilder AddElasticOpenTelemetry(this IServiceCollection services, ElasticOpenTelemetryBuilderOptions options)
	{
		var descriptor = services.SingleOrDefault(s => s.ServiceType == typeof(ElasticOpenTelemetryBuilder));

		if (descriptor?.ImplementationInstance is ElasticOpenTelemetryBuilder builder)
		{
			builder.Logger.LogWarning($$"""{{nameof(AddElasticOpenTelemetry)}} was called more than once {StackTrace}""", Environment.StackTrace.TrimStart());
			return builder;
		}

		options = options.Services is null ? options with { Services = services } : options;
		return new ElasticOpenTelemetryBuilder(options);
	}
}
