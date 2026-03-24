// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

// Minimal console app for Docker-based integration testing of the full
// EDOT Bootstrap → OpAmp → CentralConfiguration path.
// Environment variables control OpAmp endpoint, service name, log targets, etc.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

try
{
	var builder = Host.CreateApplicationBuilder(args);
	builder.Services.AddElasticOpenTelemetry();

	using var host = builder.Build();
	await host.StartAsync().ConfigureAwait(false);

	// Give hosted services time to fully initialize
	await Task.Delay(500).ConfigureAwait(false);

	await host.StopAsync().ConfigureAwait(false);

	Console.WriteLine("BOOTSTRAP_COMPLETE");
	return 0;
}
catch (Exception ex)
{
	Console.Error.WriteLine($"BOOTSTRAP_FAILED: {ex}");
	return 1;
}
