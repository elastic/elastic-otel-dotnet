// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.OpenTelemetry;

/// <summary>
/// Options to configure the OpAmp client used for central configuration.
/// </summary>
public class OpAmpClientOptions
{
	/// <summary>
	/// The server endpoint for the OpenTelemetry Protocol (OTLP) over HTTP.
	/// </summary>
	/// <remarks>
	/// <para>
	///   This should be a gateway collector running the Elastic APM Config Extension, which connects to
	///   the Elastic Observability instance storing the central configuration.
	/// </para>
	/// <para>
	///   See <see href="https://www.elastic.co/docs/reference/opentelemetry/central-configuration">central configuration</see>
	///   documentation for more information.
	/// </para>
	/// </remarks>
	public string? Endpoint { get; init; }

	/// <summary>
	/// A comma-separated list of HTTP headers to include in requests to the OpAmp server.
	/// </summary>
	/// <remarks>
	/// The primary use case is to use this to provide authentication headers required by the OpAmp server.
	/// </remarks>
	public string? Headers { get; init; }
}
