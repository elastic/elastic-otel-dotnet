// ReSharper disable once CheckNamespace

using System.Diagnostics;

namespace OpenTelemetry.Trace;

public static class TraceBuilderProviderExtensions
{
    //TODO binder source generator on Build() to make it automatic?
    public static TracerProviderBuilder AddElastic(this TracerProviderBuilder builder)
    {
        return builder
            .AddProcessor(new TransactionIdProcessor())
            .AddProcessor(new StackTraceProcessor());
    }
}


public class TransactionIdProcessor : BaseProcessor<Activity>
{
    public override void OnStart(Activity data)
    {
        data.SetTag("transaction.id", "x");
    }
}

public class StackTraceProcessor : BaseProcessor<Activity>
{
    public override void OnStart(Activity data)
    {
        //for now always capture stacktrace on start

        var stacktrace = new StackTrace(true);
        data.SetTag("_stack_trace", stacktrace);
        base.OnStart(data);
    }
    
    public override void OnEnd(Activity data)
    {
        
        data.SetTag("on_end", "data");
        
        //for now always capture stacktrace on start
        var stacktrace = data.GetTagItem("_stack_trace") as StackTrace;
        data.SetTag("_stack_trace", null);
        if (stacktrace == null) return;
        if (data.Duration < TimeSpan.FromMilliseconds(2)) return;

        data.SetTag("stacktrace", stacktrace);
    }
}
