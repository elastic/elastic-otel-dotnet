// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Microsoft.Extensions.DependencyInjection;

/// <summary> TODO </summary>
public static class OpenTelemetryServicesExtensions
{
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
	// ReSharper disable RedundantNameQualifier
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
	// ReSharper disable RedundantNameQualifier
	public static global::OpenTelemetry.IOpenTelemetryBuilder AddOpenTelemetry(
		this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services
		, params string[]? activitySourceNames
	) => services.AddElasticOpenTelemetry(activitySourceNames);
	// ReSharper enable RedundantNameQualifier
}
