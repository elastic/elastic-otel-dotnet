// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Reflection;

namespace Elastic.OpenTelemetry.Core.Configuration;

internal static class OpAmpIsolationInitializer
{
    private static bool _initialized;
    private static readonly object LockObject = new();

    public static void Initialize()
    {
        if (_initialized)
            return;

        lock (LockObject)
        {
            if (_initialized)
                return;

#if USE_ISOLATED_OPAMP_CLIENT
                TryInitializeIsolation();
#endif
            _initialized = true;
        }
    }

#if USE_ISOLATED_OPAMP_CLIENT
    private static void TryInitializeIsolation()
    {
        try
        {
            var context = IsolatedOpAmpLoadContext.GetOrCreate();
            try { context.LoadFromAssemblyName(new AssemblyName("Google.Protobuf")); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"OpAmpIsolationInitializer: Failed to pre-load Google.Protobuf: {ex.Message}"); }
            try { context.LoadFromAssemblyName(new AssemblyName("OpenTelemetry.OpAmp.Client")); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"OpAmpIsolationInitializer: Failed to pre-load OpenTelemetry.OpAmp.Client: {ex.Message}"); }
            System.Diagnostics.Debug.WriteLine("OpAmpIsolationInitializer: OpAmp dependencies loaded in isolated context");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OpAmpIsolationInitializer: Failed to initialize OpAmp isolation: {ex.Message}");
        }
    }
#endif
}
