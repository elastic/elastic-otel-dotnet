// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extensions for <see cref="IServiceCollection"/> to add OpenTelemetry services using Elastic defaults.
/// </summary>
// ReSharper disable once CheckNamespace
public static class OpenTelemetryServicesExtensions
{
	// ReSharper disable RedundantNameQualifier
#pragma warning disable IDE0001
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

	// ReSharper enable RedundantNameQualifier
#pragma warning restore IDE0001
}
