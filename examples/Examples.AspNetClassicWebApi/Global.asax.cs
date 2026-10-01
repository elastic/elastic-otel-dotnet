// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Examples.AspNetClassicWebApi;

public class WebApiApplication : HttpApplication
{
	private const string SourceName = "Example.AspNetClassic";

	internal static readonly ActivitySource ActivitySource = new(SourceName);

	private TracerProvider _tracerProvider;

	protected void Application_Start()
	{
		GlobalConfiguration.Configure(WebApiConfig.Register);
		FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);

		_tracerProvider = Sdk.CreateTracerProviderBuilder()
			.ConfigureResource(r => r.AddService("aspnet-classic-webapi-example"))
			.AddAspNetInstrumentation()
			.AddSource(SourceName)
			.WithElasticDefaults()
			.Build();
	}

	protected void Application_End() => _tracerProvider?.Dispose();
}
