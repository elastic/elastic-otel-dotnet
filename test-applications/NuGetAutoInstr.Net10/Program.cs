// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

// NuGet auto-instrumentation consumer — references Elastic.OpenTelemetry.AutoInstrumentation
// as a PackageReference. The profiler loads the plugin from the app's published output.
// No explicit EDOT API calls — the profiler injects everything via env vars.

Console.WriteLine("APP_STARTED");
await Task.Delay(5000).ConfigureAwait(false);
Console.WriteLine("APP_COMPLETE");
