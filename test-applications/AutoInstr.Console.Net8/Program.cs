// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

// Plain console app — no Elastic or OpenTelemetry references.
// The auto-instrumentation profiler injects EDOT at runtime via env vars
// (CORECLR_ENABLE_PROFILING, CORECLR_PROFILER, etc.).

Console.WriteLine("APP_STARTED");
await Task.Delay(30_000).ConfigureAwait(false);
Console.WriteLine("APP_COMPLETE");
