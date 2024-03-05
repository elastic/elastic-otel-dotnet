// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Runtime.CompilerServices;
using Elastic.OpenTelemetry;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;

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
		serviceCollection.AddElasticOpenTelemetry(logger: null);

	/// <summary>
	/// TODO
	/// </summary>
	/// <param name="serviceCollection"></param>
	/// <param name="activitySourceNames"></param>
	/// <returns></returns>
	public static IOpenTelemetryBuilder AddElasticOpenTelemetry(this IServiceCollection serviceCollection, params string[]? activitySourceNames) =>
		serviceCollection.AddElasticOpenTelemetry(logger: null, activitySourceNames);

	/// <summary>
	/// TODO
	/// </summary>
	/// <param name="serviceCollection"></param>
	/// <param name="logger"></param>
	/// <param name="activitySourceNames"></param>
	/// <returns></returns>
	public static IOpenTelemetryBuilder AddElasticOpenTelemetry(this IServiceCollection serviceCollection, ILogger? logger, params string[]? activitySourceNames)
	{
        if (serviceCollection.Any(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(ElasticOtelDistroService)))
        {
			var sp = serviceCollection.BuildServiceProvider();
			return sp.GetService<AgentBuilder>()!; //already registered as singleton
		}
		return new AgentBuilder(logger, services: serviceCollection, activitySourceNames ?? []);
	}


}
