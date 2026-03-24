// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

// Plain .NET Framework console app — no Elastic or OpenTelemetry references.
// The auto-instrumentation profiler injects EDOT at runtime via env vars
// (COR_ENABLE_PROFILING, COR_PROFILER, etc.).

using System;
using System.Threading;

namespace AutoInstr.Console.Net462
{
	internal static class Program
	{
		private static void Main()
		{
			System.Console.WriteLine("APP_STARTED");
			Thread.Sleep(5000);
			System.Console.WriteLine("APP_COMPLETE");
		}
	}
}
