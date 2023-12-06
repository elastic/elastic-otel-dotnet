using System.Diagnostics;

namespace Elastic.OpenTelemetry;

public interface IAgent : IDisposable
{
    Resource Resource { get; }
    ActivitySource ActivitySource { get; }
}
