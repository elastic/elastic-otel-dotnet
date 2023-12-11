namespace Elastic.OpenTelemetry.SemanticConventions;

internal static class ResourceSemanticConventions
{
    public const string AttributeDeploymentEnvironment = "deployment.environment";

    public const string AttributeServiceName = "service.name";
    public const string AttributeServiceVersion = "service.version";

    public const string AttributeTelemetryDistroName = "telemetry.distro.name";
    public const string AttributeTelemetryDistroVersion = "telemetry.distro.version";
}
