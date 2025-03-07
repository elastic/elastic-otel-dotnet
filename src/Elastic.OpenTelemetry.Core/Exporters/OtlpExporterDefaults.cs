// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Core;
using OpenTelemetry.Exporter;

#if NETFRAMEWORK
using System.Net.Http;
#endif

namespace Elastic.OpenTelemetry.Exporters;

internal static class OtlpExporterDefaults
{
	internal static readonly HttpMessageHandler Handler = new ElasticUserAgentHandler($"elastic-otlp-dotnet/{VersionHelper.InformationalVersion}");

	public static void OtlpExporterOptions(OtlpExporterOptions options) =>
		options.HttpClientFactory = () =>
		{
			var client = new HttpClient(Handler);
			return client;
		};
}
