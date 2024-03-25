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
/// Extension methods for <see cref="IServiceProvider"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	///
	/// </summary>
	/// <param name="serviceCollection"></param>
	/// <returns></returns>
	public static IOpenTelemetryBuilder AddElasticOpenTelemetry(this IServiceCollection serviceCollection) =>
		serviceCollection.AddElasticOpenTelemetry(new ElasticOpenTelemetryOptions { Services = serviceCollection });

	/// <summary>
	/// TODO
	/// </summary>
	/// <param name="serviceCollection"></param>
	/// <param name="activitySourceNames"></param>
	/// <returns></returns>
	public static IOpenTelemetryBuilder AddElasticOpenTelemetry(this IServiceCollection serviceCollection, params string[]? activitySourceNames) =>
		serviceCollection.AddElasticOpenTelemetry(new ElasticOpenTelemetryOptions { Services = serviceCollection, ActivitySources = activitySourceNames ?? [] });

	/// <summary>
	/// TODO
	/// </summary>
	/// <param name="serviceCollection"></param>
	/// <param name="options"><see cref="ElasticOpenTelemetryOptions"/></param>
	/// <returns></returns>
	public static IOpenTelemetryBuilder AddElasticOpenTelemetry(this IServiceCollection serviceCollection, ElasticOpenTelemetryOptions options)
	{
		if (serviceCollection.Any(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(ElasticOtelDistroService)))
		{
			// TODO - Can we avoid this by storing the instance on the builder (internal access)
			var sp = serviceCollection.BuildServiceProvider();
			return sp.GetService<ElasticOpenTelemetryBuilder>()!; //already registered as singleton
		}
		return new ElasticOpenTelemetryBuilder(options);
	}
}
