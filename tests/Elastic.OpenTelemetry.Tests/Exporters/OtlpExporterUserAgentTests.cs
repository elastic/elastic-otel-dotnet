// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Net;
using Elastic.OpenTelemetry.Exporters;
using OpenTelemetry;
using OpenTelemetry.Exporter;

namespace Elastic.OpenTelemetry.Tests.Exporters;

public class OtlpExporterUserAgentTests
{
	[Fact]
	public void ConfigureElasticUserAgent_SetsProductIdentifier()
	{
		var options = new OtlpExporterOptions();

		options.ConfigureElasticUserAgent();

		Assert.StartsWith("elastic-otlp-dotnet/", options.UserAgentProductIdentifier);
	}

	[Fact]
	public async Task ExportedRequests_IncludeElasticUserAgent()
	{
		using var listener = new HttpListener();
		var port = GetFreePort();
		var prefix = $"http://localhost:{port}/";
		listener.Prefixes.Add(prefix);
		listener.Start();

		var userAgentTask = Task.Run(async () =>
		{
			var context = await listener.GetContextAsync().ConfigureAwait(false);
			var userAgent = context.Request.Headers["User-Agent"];
			context.Response.StatusCode = 200;
			context.Response.ContentType = "application/x-protobuf";
			context.Response.Close();
			return userAgent;
		});

		using var source = new ActivitySource("otlp-ua-test");
		using (var provider = Sdk.CreateTracerProviderBuilder()
			.AddSource("otlp-ua-test")
			.AddOtlpExporter(o =>
			{
				o.Endpoint = new Uri($"{prefix}v1/traces");
				o.Protocol = OtlpExportProtocol.HttpProtobuf;
				o.ConfigureElasticUserAgent();
			})
			.Build())
		{
			using (source.StartActivity("test"))
			{ }
			provider!.ForceFlush(10_000);
		}

		var completed = await Task.WhenAny(userAgentTask, Task.Delay(TimeSpan.FromSeconds(10)));
		Assert.Same(userAgentTask, completed);

		var userAgent = await userAgentTask;
		Assert.StartsWith("elastic-otlp-dotnet/", userAgent);
		Assert.Contains("OTel-OTLP-Exporter-Dotnet", userAgent);
	}

	private static int GetFreePort()
	{
		var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
		l.Start();
		var port = ((IPEndPoint)l.LocalEndpoint).Port;
		l.Stop();
		return port;
	}
}
