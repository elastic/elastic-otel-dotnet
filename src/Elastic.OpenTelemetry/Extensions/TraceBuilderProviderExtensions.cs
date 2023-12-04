using Elastic.OpenTelemetry.Exporters;
using Elastic.OpenTelemetry.Processors;

using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Elastic.OpenTelemetry.Extensions;

public static class TraceBuilderProviderExtensions
{
    //TODO binder source generator on Build() to make it automatic?
    public static TracerProviderBuilder AddElastic(this TracerProviderBuilder builder)
    {
        return builder
            .AddProcessor(new TransactionIdProcessor())
            .AddProcessor(new StackTraceProcessor())
            .AddProcessor(new SpanCounterProcessor())
            .AddProcessor(new SpanCompressionProcessor())
            .AddProcessor(new BatchActivityExportProcessor(new BatchExporter()));
    }
}
