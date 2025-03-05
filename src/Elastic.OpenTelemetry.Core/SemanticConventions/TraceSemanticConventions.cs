// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.OpenTelemetry.SemanticConventions;

internal static class TraceSemanticConventions
{
	// HTTP
	public const string HttpScheme = "http.scheme";
	public const string HttpTarget = "http.target";

	// NET
	public const string NetHostName = "net.host.name";
	public const string NetHostPort = "net.host.port";

	// SERVER
	public const string ServerAddress = "server.address";
	public const string ServerPort = "server.port";

	// URL
	public const string UrlPath = "url.path";
	public const string UrlQuery = "url.query";
	public const string UrlScheme = "url.scheme";
}
