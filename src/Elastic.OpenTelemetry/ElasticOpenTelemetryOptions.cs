// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry;

/// <summary>
/// Expert options to provide to <see cref="ElasticOpenTelemetryBuilder"/> to control its initial OpenTelemetry registration.
/// </summary>
public record ElasticOpenTelemetryOptions
{
	/// <summary>
	/// Provide an additional logger to the internal file logger.
	/// <para>
	/// The agent will always log to file if a path is provided using the <c>ELASTIC_OTEL_LOG_DIRECTORY</c>.
	/// environment variable.</para>
	/// </summary>
	public ILogger? Logger { get; init; }

	/// <summary>
	/// Provides an <see cref="IServiceCollection"/> to register the agent into.
	/// If null, a new local instance will be used.
	/// </summary>
	public IServiceCollection? Services { get; init; }

	/// <summary>
	/// Stops <see cref="ElasticOpenTelemetryBuilder"/> from registering OLTP exporters, useful for testing scenarios.
	/// </summary>
	public bool SkipOtlpExporter { get; init; }

	/// <summary>
	/// Optional name which is used when retrieving OTLP options.
	/// </summary>
	public string? OtlpExporterName { get; init; }
}
