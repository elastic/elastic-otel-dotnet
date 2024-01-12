using Example.Elastic.OpenTelemetry.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

builder.Services.AddOtelElasticAgent("CustomActivitySource");

var host = builder.Build();
host.Run();
