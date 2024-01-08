// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information
namespace Elastic.OpenTelemetry.SemanticConventions;

internal static class ResourceSemanticConventions
{
    public const string AttributeDeploymentEnvironment = "deployment.environment";

    public const string AttributeServiceName = "service.name";
    public const string AttributeServiceVersion = "service.version";

    public const string AttributeTelemetryDistroName = "telemetry.distro.name";
    public const string AttributeTelemetryDistroVersion = "telemetry.distro.version";
}
