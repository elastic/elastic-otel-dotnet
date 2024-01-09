// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information
using Elastic.OpenTelemetry.SemanticConventions;
using OpenTelemetry.Resources;
namespace Elastic.OpenTelemetry.Resources;

internal class ElasticEnvironmentVariableDetector : IResourceDetector
{
    // NOTE: It might be nice to follow the same pattern as `AddEnvironmentVariableDetector`, reading from
    // the IServiceProvider is available and falling back to building IConfiguration manually.
    // The API surface doesn't make that practical though as it relies on an internal method.
    // TODO - We should have a version that can use the IServiceProvider and IConfiguration when the SDK
    // is registered via DI.

    private string? _serviceName;
    private string? _serviceVersion;
    private string? _serviceEnvironment;

    public Resource Detect()
    {
        _serviceName ??= GetServiceName();
        _serviceVersion ??= GetServiceVersion();
        _serviceEnvironment ??= GetServiceEnvironment();

        var resource = Resource.Empty;

        if (string.IsNullOrEmpty(_serviceName) && string.IsNullOrEmpty(_serviceVersion) && string.IsNullOrEmpty(_serviceEnvironment))
            return resource;

        var attributes = new Dictionary<string, object>();

        if (!string.IsNullOrEmpty(_serviceName))
            attributes.Add(ResourceSemanticConventions.AttributeServiceName, _serviceName);

        if (!string.IsNullOrEmpty(_serviceVersion))
            attributes.Add(ResourceSemanticConventions.AttributeServiceVersion, _serviceVersion);

        if (!string.IsNullOrEmpty(_serviceEnvironment))
            attributes.Add(ResourceSemanticConventions.AttributeDeploymentEnvironment, _serviceEnvironment);

        resource = new Resource(attributes);

        return resource;

        static string? GetServiceName() => Environment.GetEnvironmentVariable("ELASTIC_APM_SERVICE_NAME");
        static string? GetServiceVersion() => Environment.GetEnvironmentVariable("ELASTIC_APM_SERVICE_VERSION");
        static string? GetServiceEnvironment() => Environment.GetEnvironmentVariable("ELASTIC_APM_ENVIRONMENT");
    }
}
