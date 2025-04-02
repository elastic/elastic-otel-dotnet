// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Example.WorkerService;
using OpenTelemetry;

var builder = Host.CreateApplicationBuilder(args);

builder.AddElasticOpenTelemetry(b => b
	.WithTracing(t => t.AddSource(Worker.DiagnosticName))
	.WithMetrics(m => m.AddMeter(Worker.DiagnosticName)));

builder.Services.AddSingleton<QueueReader>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
