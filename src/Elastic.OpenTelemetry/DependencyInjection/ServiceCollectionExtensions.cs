// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information
using Elastic.OpenTelemetry;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for <see cref="IServiceProvider"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// TODO
	/// </summary>
	/// <param name="builder"></param>
	/// <returns></returns>
	public static IHostApplicationBuilder AddElasticOpenTelemetry(this IHostApplicationBuilder builder) =>
		AddElasticOpenTelemetry(builder, []);

	/// <summary>
	/// TODO
	/// </summary>
	/// <param name="builder"></param>
	/// <param name="activitySourceNames"></param>
	/// <returns></returns>
	public static IHostApplicationBuilder AddElasticOpenTelemetry(this IHostApplicationBuilder builder, params string[] activitySourceNames)
	{
		builder.Services.AddElasticOpenTelemetry(activitySourceNames);
		return builder;
	}

	/// <summary>
	/// TODO
	/// </summary>
	/// <param name="builder"></param>
	/// <param name="configureTracerProvider"></param>
	/// <param name="configureMeterProvider"></param>
	/// <returns></returns>
	public static IHostApplicationBuilder AddElasticOpenTelemetry(
		this IHostApplicationBuilder builder,
		Action<TracerProviderBuilder>? configureTracerProvider,
		Action<MeterProviderBuilder>? configureMeterProvider)
	{
		builder.Services.AddElasticOpenTelemetry(configureTracerProvider, configureMeterProvider);
		return builder;
	}


	/// <summary>
	/// Adds the Elastic OpenTelemetry distribution to an application via the <see cref="IServiceCollection"/>.
	/// </summary>
	/// <param name="serviceCollection">TODO</param>
	/// <returns>TODO</returns>
	public static IServiceCollection AddElasticOpenTelemetry(this IServiceCollection serviceCollection) =>
		new AgentBuilder().Register(serviceCollection);

	/// <summary>
	/// TODO
	/// </summary>
	/// <param name="serviceCollection"></param>
	/// <param name="activitySourceNames"></param>
	/// <returns></returns>
	public static IServiceCollection AddElasticOpenTelemetry(this IServiceCollection serviceCollection, params string[] activitySourceNames) =>
		new AgentBuilder(activitySourceNames).Register(serviceCollection);

	/// <summary>
	/// TODO
	/// </summary>
	/// <param name="serviceCollection"></param>
	/// <param name="configureTracerProvider"></param>
	/// <param name="configureMeterProvider"></param>
	/// <returns></returns>
	public static IServiceCollection AddElasticOpenTelemetry(
		this IServiceCollection serviceCollection,
		Action<TracerProviderBuilder>? configureTracerProvider,
		Action<MeterProviderBuilder>? configureMeterProvider)
	{
		var builder = new AgentBuilder();

		if (configureTracerProvider is not null)
			builder.ConfigureTracer(configureTracerProvider);

		if (configureMeterProvider is not null)
			builder.ConfigureMeter(configureMeterProvider);

		return builder.Register(serviceCollection);
	}
}
