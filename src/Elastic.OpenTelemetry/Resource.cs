namespace Elastic.OpenTelemetry;

/// <summary>
/// A resource represents the entity producing telemetry as resource attributes.
/// </summary>
/// <param name="serviceName"></param>
/// <param name="version"></param>
public class Resource(string serviceName, string version)
{
    // TODO - Should we remove this and rely on the underlying Otel Resource detection so that we don't invent our own concept?

    private static Resource? _calculatedService;

    // TODO - Should we accept a `System.Version` instance overload.

    /// <summary>
    /// The name of the service being observed.
    /// </summary>
    public string ServiceName { get; } = serviceName;

    /// <summary>
    /// The version of the service being observed.
    /// </summary>
    public string Version { get; } = version;

    /// <summary>
    /// Produces a <see cref="Resource"/> instance configured using default values.
    /// </summary>
    public static Resource Default
    {
        get
        {
            if (_calculatedService != null) return _calculatedService;

            // hardcoded for now
            // todo - load from OTEL environment variables
            var name = "Example.Elastic.OpenTelemetry";
            var version = "1.0.0";
            _calculatedService = new Resource(name, version);
            return _calculatedService;
        }
    }
}
