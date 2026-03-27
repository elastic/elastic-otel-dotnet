// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

// NuGet consumer test app — references Elastic.OpenTelemetry as a PackageReference.
// Built at test time against a local NuGet feed containing freshly packed .nupkg files.
// Configures EDOT via env vars (OpAmp endpoint, log targets, etc.).

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

try
{
	var builder = Host.CreateApplicationBuilder(args);
	builder.Services.AddElasticOpenTelemetry();

	using var host = builder.Build();
	await host.StartAsync().ConfigureAwait(false);

	// Allow time for EDOT to initialize and OpAmp client to connect + receive config.
	// OpAmp start timeout is 2000ms; this gives ample buffer.
	await Task.Delay(5000).ConfigureAwait(false);

	await host.StopAsync().ConfigureAwait(false);

	Console.WriteLine("APP_COMPLETE");
	return 0;
}
catch (Exception ex)
{
	Console.Error.WriteLine($"APP_FAILED: {ex}");
	return 1;
}
