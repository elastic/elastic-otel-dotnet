// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

// NuGet consumer test app for .NET Framework — references Elastic.OpenTelemetry
// as a PackageReference. Built at test time against a local NuGet feed.

using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace NuGetConsumer.Net462
{
	internal static class Program
	{
		private static void Main(string[] args)
		{
			try
			{
				var builder = Host.CreateApplicationBuilder(args);
				builder.Services.AddElasticOpenTelemetry();

				using var host = builder.Build();
				host.Start();

				// Allow time for EDOT to initialize and OpAmp client to connect + receive config.
				Thread.Sleep(5000);

				host.StopAsync().GetAwaiter().GetResult();

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
