// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

// NuGet consumer test app for .NET Framework 4.6.2
// =================================================
// This app exercises the manual OpenTelemetry SDK builder APIs on net462:
//   Sdk.CreateTracerProviderBuilder().WithElasticDefaults()
//   Sdk.CreateMeterProviderBuilder().WithElasticDefaults()
//
// This is the realistic consumption pattern for legacy .NET Framework apps
// that do not use the Generic Host or dependency injection. Most net462
// applications in the wild would wire up OpenTelemetry this way.
//
// NOTE: We intentionally do NOT use Host.CreateApplicationBuilder here.
// While that API is available on net462 (via the Microsoft.Extensions.Hosting
// NuGet package targeting netstandard2.0), it's not representative of how
// most legacy apps work. The hosting/DI path (AddElasticOpenTelemetry) is
// already tested by the NuGetConsumer.Net8 app on a modern runtime.
//
// Built at test time against a local NuGet feed containing freshly packed .nupkg files.
// Configures EDOT via env vars (OpAmp endpoint, log targets, etc.).

using System;
using System.Threading;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace NuGetConsumer.Net462
{
	internal static class Program
	{
		private static void Main()
		{
			try
			{
				// Manual builder APIs — no hosting, no DI.
				// This is the typical pattern for legacy .NET Framework applications.
				using var tracerProvider = Sdk.CreateTracerProviderBuilder()
					.WithElasticDefaults()
					.Build();

				using var meterProvider = Sdk.CreateMeterProviderBuilder()
					.WithElasticDefaults()
					.Build();

				// Allow time for EDOT to initialize and OpAmp client to connect + receive config.
				// OpAmp start timeout is 2000ms; this gives ample buffer.
				Thread.Sleep(5000);

				Console.WriteLine("APP_COMPLETE");
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine($"APP_FAILED: {ex}");
				Environment.ExitCode = 1;
			}
		}
	}
}
