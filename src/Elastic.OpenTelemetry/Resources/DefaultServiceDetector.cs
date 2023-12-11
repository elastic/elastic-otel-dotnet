using System.Diagnostics;
using Elastic.OpenTelemetry.SemanticConventions;
using OpenTelemetry.Resources;

namespace Elastic.OpenTelemetry.Resources;

internal class DefaultServiceDetector : IResourceDetector
{
    private static readonly Resource DefaultResource;

    static DefaultServiceDetector()
    {
        // This is replicated from https://github.com/open-telemetry/opentelemetry-dotnet/blob/00750ddcca5c2819238d5b8bda10753f58ba4a7a/src/OpenTelemetry/Resources/ResourceBuilder.cs
        // as the default resource is not public.

        var defaultServiceName = "unknown_service";

        try
        {
            var processName = Process.GetCurrentProcess().ProcessName;
            if (!string.IsNullOrWhiteSpace(processName))
            {
                defaultServiceName = $"{defaultServiceName}:{processName}";
            }
        }
        catch
        {
            // GetCurrentProcess can throw PlatformNotSupportedException
        }

        DefaultResource = new Resource(new Dictionary<string, object>
        {
            [ResourceSemanticConventions.AttributeServiceName] = defaultServiceName,
        });
    }

    public Resource Detect() => DefaultResource;
}
