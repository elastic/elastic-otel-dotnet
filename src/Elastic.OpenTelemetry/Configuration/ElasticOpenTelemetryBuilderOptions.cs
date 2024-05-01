// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.Configuration;

/// <summary>
/// Expert options to provide to <see cref="ElasticOpenTelemetryBuilder"/> to control its initial OpenTelemetry registration.
/// </summary>
public record ElasticOpenTelemetryBuilderOptions
{
	private ElasticOpenTelemetryOptions? _elasticOpenTelemetryOptions;

	/// <summary>
	/// Provide an additional logger to the internal file logger.
	/// <para>
	/// The distribution will always log to file if a path is provided using the <c>ELASTIC_OTEL_LOG_DIRECTORY</c>.
	/// environment variable.</para>
	/// </summary>
	public ILogger? Logger { get; init; }

	/// <summary>
	/// Provides an <see cref="IServiceCollection"/> to register the <see cref="IInstrumentationLifetime"/> into.
	/// If null, a new local instance will be used.
	/// </summary>
	internal IServiceCollection? Services { get; init; }

	/// <summary>
	/// Advanced options which can be used to finely-tune the behaviour of the Elastic
	/// distribution of OpenTelemetry.
	/// </summary>
	public ElasticOpenTelemetryOptions DistroOptions
	{
		get => _elasticOpenTelemetryOptions ?? new();
		init => _elasticOpenTelemetryOptions = value;
	}
}
