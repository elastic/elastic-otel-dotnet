// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;

/// <summary> TODO </summary>
// ReSharper disable once CheckNamespace
public static class OpenTelemetryServicesExtensions
{
	// ReSharper disable RedundantNameQualifier

	/// <summary>
	/// <inheritdoc cref="Microsoft.Extensions.DependencyInjection.OpenTelemetryServicesExtensions.AddOpenTelemetry" path="summary"/>
	/// <para>Uses defaults particularly well suited for Elastic's Observability offering because Elastic.OpenTelemetry is referenced</para>
	/// </summary>
	/// <remarks>
	/// <inheritdoc cref="Microsoft.Extensions.DependencyInjection.OpenTelemetryServicesExtensions.AddOpenTelemetry" path="remarks"/>
	/// </remarks>
	/// <param name="services"><see cref="IServiceCollection"/></param>
	/// <returns>
	/// <inheritdoc cref="Microsoft.Extensions.DependencyInjection.OpenTelemetryServicesExtensions.AddOpenTelemetry" path="returns"/>
	/// </returns>
	public static global::OpenTelemetry.IOpenTelemetryBuilder AddOpenTelemetry(
		this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services
	) => services.AddElasticOpenTelemetry();

	/// <summary>
	/// <inheritdoc cref="Microsoft.Extensions.DependencyInjection.OpenTelemetryServicesExtensions.AddOpenTelemetry" path="summary"/>
	/// <para>Uses defaults particularly well suited for Elastic's Observability offering because Elastic.OpenTelemetry is referenced</para>
	/// </summary>
	/// <remarks>
	/// <inheritdoc cref="Microsoft.Extensions.DependencyInjection.OpenTelemetryServicesExtensions.AddOpenTelemetry" path="remarks"/>
	/// </remarks>
	/// <param name="services"><see cref="IServiceCollection"/></param>
	/// <param name="activitySourceNames">Activity source names to subscribe too</param>
	/// <returns>
	/// <inheritdoc cref="Microsoft.Extensions.DependencyInjection.OpenTelemetryServicesExtensions.AddOpenTelemetry" path="returns"/>
	/// </returns>
	public static global::OpenTelemetry.IOpenTelemetryBuilder AddOpenTelemetry(
		this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services
		, params string[]? activitySourceNames
	) => services.AddElasticOpenTelemetry(activitySourceNames);

	/// <summary>
	/// <inheritdoc cref="Microsoft.Extensions.DependencyInjection.OpenTelemetryServicesExtensions.AddOpenTelemetry" path="summary"/>
	/// <para>Uses defaults particularly well suited for Elastic's Observability offering because Elastic.OpenTelemetry is referenced</para>
	/// </summary>
	/// <remarks>
	/// <inheritdoc cref="Microsoft.Extensions.DependencyInjection.OpenTelemetryServicesExtensions.AddOpenTelemetry" path="remarks"/>
	/// </remarks>
	/// <param name="services"><see cref="IServiceCollection"/></param>
	/// <param name="options">Expert level options to control the bootstrapping of the Elastic Agent</param>
	/// <returns>
	/// <inheritdoc cref="Microsoft.Extensions.DependencyInjection.OpenTelemetryServicesExtensions.AddOpenTelemetry" path="returns"/>
	/// </returns>
	public static global::OpenTelemetry.IOpenTelemetryBuilder AddOpenTelemetry(
		this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services
		, AgentBuilderOptions options
	)
	{
		if (options.Services == null)
			options = options with { Services = services };
		return services.AddElasticOpenTelemetry(options);
	}

	// ReSharper enable RedundantNameQualifier
}
