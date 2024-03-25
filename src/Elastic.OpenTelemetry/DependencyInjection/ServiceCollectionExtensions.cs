// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry;
using Elastic.OpenTelemetry.Hosting;
using Microsoft.Extensions.Hosting;
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
	/// <param name="serviceCollection">The <see cref="IServiceCollection"/> for adding services.</param>
	/// <returns>
	/// An instance of <see cref="IOpenTelemetryBuilder"/> that can be used to further configure the
	/// OpenTelemetry SDK.
	/// </returns>
	public static IOpenTelemetryBuilder AddElasticOpenTelemetry(this IServiceCollection serviceCollection) =>
		serviceCollection.AddElasticOpenTelemetry(new ElasticOpenTelemetryOptions { Services = serviceCollection });

	/// <summary>
	/// Registers the Elastic OpenTelemetry builder with the provided <see cref="IServiceCollection"/>.
	/// </summary>	
	/// <param name="serviceCollection">The <see cref="IServiceCollection"/> for adding services.</param>
	/// <param name="options"><see cref="ElasticOpenTelemetryOptions"/> for the initial OpenTelemetry registration.</param>
	/// <returns>
	/// An instance of <see cref="IOpenTelemetryBuilder"/> that can be used to further configure the
	/// OpenTelemetry SDK.
	/// </returns>
	public static IOpenTelemetryBuilder AddElasticOpenTelemetry(this IServiceCollection serviceCollection, ElasticOpenTelemetryOptions options)
	{
		if (serviceCollection.Any(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(ElasticOpenTelemetryService)))
		{
			// TODO - Can we avoid this by storing the instance on the builder (internal access)
			var sp = serviceCollection.BuildServiceProvider();
			return sp.GetService<ElasticOpenTelemetryBuilder>()!; //already registered as singleton
		}
		return new ElasticOpenTelemetryBuilder(options);
	}
}
