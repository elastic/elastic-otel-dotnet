// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Example.WorkerService;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddElasticOpenTelemetry(Worker.ActivitySourceName, "CustomMeter")
	.ConfigureResource(r => r.AddService(serviceName: "MyService"))
	.WithTracing(t => t.AddConsoleExporter())
	.WithMetrics(m => m.AddConsoleExporter());

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
