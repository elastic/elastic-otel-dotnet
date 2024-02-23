// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information
using Elastic.OpenTelemetry;
using Elastic.OpenTelemetry.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for <see cref="IServiceProvider"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary> TODO </summary>
	public static IHostApplicationBuilder AddElasticOpenTelemetry(this IHostApplicationBuilder builder) =>
		builder.AddElasticOpenTelemetry([]);

	/// <summary> </summary>
	public static IHostApplicationBuilder AddElasticOpenTelemetry(this IHostApplicationBuilder builder, params string[] activitySourceNames)
	{
		builder.Services.AddElasticOpenTelemetry(activitySourceNames);
		return builder;
	}

	/// <summary>
	///
	/// </summary>
	/// <param name="serviceCollection"></param>
	/// <returns></returns>
	public static AgentBuilder AddElasticOpenTelemetry(this IServiceCollection serviceCollection) =>
		serviceCollection.AddElasticOpenTelemetry(null);

	/// <summary>
	/// TODO
	/// </summary>
	/// <param name="serviceCollection"></param>
	/// <param name="activitySourceNames"></param>
	/// <returns></returns>
	public static AgentBuilder AddElasticOpenTelemetry(this IServiceCollection serviceCollection, params string[]? activitySourceNames)
	{
		//TODO return IAgentBuilder that does not expose Build()
		var builder = new AgentBuilder(activitySourceNames ?? []);
		serviceCollection
			.AddHostedService<ElasticOtelDistroService>()
			.AddSingleton<LoggerResolver>()
			.AddSingleton(builder)
			.AddOpenTelemetry();
		return builder;
	}

}
