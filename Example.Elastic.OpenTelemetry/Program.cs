// See https://aka.ms/new-console-template for more information

using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var serviceName = "Example.Elastic.OpenTelemetry";
var serviceVersion = "1.0.0";

var activitySource = new ActivitySource(serviceName);

Console.WriteLine("Hello, World!");

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource(serviceName)
    .ConfigureResource(resource =>
        resource.AddService(
            serviceName: serviceName,
            serviceVersion: serviceVersion))
    .AddConsoleExporter()
    .AddElastic()
    .Build();

var tracer = tracerProvider.GetTracer(serviceName, serviceVersion);

for (var i = 0; i < 2; i++)
{
    using var parent = activitySource.StartActivity("Parent");
    await Task.Delay(TimeSpan.FromMilliseconds(10));
    StartChildSpan();
}


await Task.Delay(TimeSpan.FromSeconds(5));


//tracerProvider.ForceFlush((int)TimeSpan.FromSeconds(5).TotalMilliseconds);


void StartChildSpan()
{
    using var child = activitySource.StartActivity("Child");
}


