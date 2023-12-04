using OpenTelemetry;
using System.Diagnostics;

namespace Elastic.OpenTelemetry.Processors;

public class StackTraceProcessor : BaseProcessor<Activity>
{
    public override void OnStart(Activity data)
    {
        //for now always capture stack trace on start
        var stackTrace = new StackTrace(true);
        data.SetCustomProperty("_stack_trace", stackTrace);
        base.OnStart(data);
    }

    public override void OnEnd(Activity data)
    {
        if (data.GetCustomProperty("_stack_trace") is not StackTrace stackTrace) return;
        if (data.Duration < TimeSpan.FromMilliseconds(2)) return;

        data.SetTag("code.stacktrace", stackTrace);
        base.OnEnd(data);
    }
}
