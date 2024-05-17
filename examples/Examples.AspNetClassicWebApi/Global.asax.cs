// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Web.Http;
using System.Web.Mvc;
using Elastic.OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using OpenTelemetry;
using System.Web;
using Elastic.OpenTelemetry.Extensions;
using System.Diagnostics;

namespace Examples.AspNetClassicWebApi;

public class WebApiApplication : HttpApplication
{
	private const string SourceName = "Example.AspNetClassic";

	internal static readonly ActivitySource ActivitySource = new(SourceName);

	private IInstrumentationLifetime _lifetime;

	protected void Application_Start()
	{
		GlobalConfiguration.Configure(WebApiConfig.Register);
		FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);

		_lifetime = new ElasticOpenTelemetryBuilder()
			.ConfigureResource(r => r.AddService("aspnet-classic-webapi-example"))
			.WithTracing(t => t
				.AddAspNetInstrumentation()
				.AddSource(SourceName))
			.Build();
	}

	protected void Application_End() => _lifetime?.Dispose();
}
