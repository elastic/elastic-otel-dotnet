// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry;
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
		var descriptor = serviceCollection.SingleOrDefault(s => s.ServiceType == typeof(ElasticOpenTelemetryBuilder));

		if (descriptor?.ImplementationInstance is not null)
		{
			var builder = (ElasticOpenTelemetryBuilder)descriptor.ImplementationInstance;
			builder.Logger.LogWarning($$"""{{nameof(AddElasticOpenTelemetry)}} was called more than once {StackTrace}""", Environment.StackTrace.TrimStart());
			return builder;
		}

		return new ElasticOpenTelemetryBuilder(options);
	}
}
