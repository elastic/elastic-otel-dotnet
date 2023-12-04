using System.Diagnostics;

namespace Elastic.OpenTelemetry;

public interface IAgent : IDisposable
{
    Resource Service { get; }
    ActivitySource ActivitySource { get; }
}
