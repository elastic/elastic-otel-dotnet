// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

// NativeAOT auto-instrumentation consumer — validates that
// Elastic.OpenTelemetry.AutoInstrumentation is trim/AOT compatible.
// Instantiating the plugin triggers EDOT bootstrap (via static constructor).
// No profiler env vars needed — the plugin is compiled in.

var plugin = new Elastic.OpenTelemetry.AutoInstrumentationPlugin();

Console.WriteLine("APP_STARTED");
await Task.Delay(5000).ConfigureAwait(false);
Console.WriteLine("APP_COMPLETE");
