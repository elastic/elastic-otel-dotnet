// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

#if NETFRAMEWORK
using System.Net.Http;
#endif

namespace Elastic.OpenTelemetry.Exporters;

internal class ElasticUserAgentHandler(string userAgent) : HttpClientHandler
{
	private readonly string _userAgent = userAgent;

	protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		request.Headers.Remove("User-Agent");
		request.Headers.Add("User-Agent", _userAgent);

		return base.SendAsync(request, cancellationToken);
	}
}
