// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry;
using OpenTelemetry.Trace;

// ReSharper disable once CheckNamespace
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.Hosting;
#pragma warning restore IDE0130 // Namespace does not match folder structure

public static class Extensions
{
	public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
	{
		builder.ConfigureOpenTelemetry();

		builder.AddDefaultHealthChecks();

		builder.Services.AddServiceDiscovery();

		builder.Services.ConfigureHttpClientDefaults(http =>
		{
			// Turn on resilience by default
			http.AddStandardResilienceHandler();

			// Turn on service discovery by default
			http.AddServiceDiscovery();
		});

		return builder;
	}

	public static IHostApplicationBuilder ConfigureOpenTelemetry(this IHostApplicationBuilder builder)
	{
		builder.Services.AddElasticOpenTelemetry(builder.Configuration)
			.WithMetrics()
			.WithTracing(tracing =>
			{
				if (builder.Environment.IsDevelopment())
				{
					// We want to view all traces in development
					tracing.SetSampler(new AlwaysOnSampler());
				}
			});

		builder.AddOpenTelemetryExporters();

		return builder;
	}

	// Leaving this in for documentation purposes
	// ReSharper disable once UnusedMethodReturnValue.Local
	private static IHostApplicationBuilder AddOpenTelemetryExporters(this IHostApplicationBuilder builder) =>
		//var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
		//if (useOtlpExporter)
		//{
		//	builder.Services.Configure<OpenTelemetryLoggerOptions>(logging => logging.AddOtlpExporter());
		//	builder.Services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddOtlpExporter());
		//	builder.Services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddOtlpExporter());
		//}
		// Uncomment the following lines to enable the Prometheus exporter (requires the OpenTelemetry.Exporter.Prometheus.AspNetCore package)
		// builder.Services.AddOpenTelemetry()
		//    .WithMetrics(metrics => metrics.AddPrometheusExporter());
		// Uncomment the following lines to enable the Azure Monitor exporter (requires the Azure.Monitor.OpenTelemetry.AspNetCore package)
		//if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
		//{
		//builder.Services.AddOpenTelemetry()
		//   .UseAzureMonitor();
		//}
		builder;

	public static IHostApplicationBuilder AddDefaultHealthChecks(this IHostApplicationBuilder builder)
	{
		builder.Services.AddHealthChecks()
			// Add a default liveness check to ensure app is responsive
			.AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

		return builder;
	}

	public static WebApplication MapDefaultEndpoints(this WebApplication app)
	{
		// Uncomment the following line to enable the Prometheus endpoint (requires the OpenTelemetry.Exporter.Prometheus.AspNetCore package)
		// app.MapPrometheusScrapingEndpoint();

		// All health checks must pass for app to be considered ready to accept traffic after starting
		app.MapHealthChecks("/health");

		// Only health checks tagged with the "live" tag must pass for app to be considered alive
		app.MapHealthChecks("/alive", new HealthCheckOptions { Predicate = r => r.Tags.Contains("live") });

		return app;
	}
}
