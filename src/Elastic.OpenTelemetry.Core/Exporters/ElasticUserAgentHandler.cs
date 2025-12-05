// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

#if NETFRAMEWORK
using System.Net.Http;
#endif

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Elastic.OpenTelemetry.Exporters;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Sets a custom User-Agent header for outgoing HTTP requests.
/// Uses DelegatingHandler with SocketsHttpHandler for .NET, and HttpClientHandler for .NET Framework.
/// </summary>
internal class ElasticUserAgentHandler
#if NET
	: DelegatingHandler
#else
	: HttpClientHandler
#endif
{
	private readonly string _userAgent;

#if NET
	public ElasticUserAgentHandler(string userAgent) : base(new SocketsHttpHandler()) => _userAgent = userAgent;
#else
	public ElasticUserAgentHandler(string userAgent) => _userAgent = userAgent;
#endif

	protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		PrepareRequest(request);
		return base.SendAsync(request, cancellationToken);
	}

#if NET
	// This method is only available in .NET targets and is crucial to override for synchronous calls.
	// The upstream SDK prefers synchronous calls in many scenarios and without this override, the User-Agent header would not be set correctly.
	protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		PrepareRequest(request);
		return base.Send(request, cancellationToken);
	}
#endif

	private void PrepareRequest(HttpRequestMessage request)
	{
		request.Headers.Remove("User-Agent");
		request.Headers.Add("User-Agent", _userAgent);
	}
}
