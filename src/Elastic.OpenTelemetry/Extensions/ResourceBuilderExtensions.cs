using Elastic.OpenTelemetry.SemanticConventions;

using OpenTelemetry.Resources;

namespace Elastic.OpenTelemetry.Extensions;

internal static class ResourceBuilderExtensions
{
    internal static ResourceBuilder AddDistroAttributes(this ResourceBuilder builder) =>
        builder.AddAttributes(new Dictionary<string, object>
        {
            { ResourceSemanticConventions.AttributeTelemetryDistroName, "elastic-dotnet" },
            { ResourceSemanticConventions.AttributeTelemetryDistroVersion, Agent.InformationalVersion }
        });
}
