using Elastic.OpenTelemetry.Processors;
using OpenTelemetry.Exporter;
using OpenTelemetry.Trace;

namespace Elastic.OpenTelemetry.Extensions;

public static class TraceBuilderProviderExtensions
{
    //TODO binder source generator on Build() to make it automatic?
    public static TracerProviderBuilder AddElasticProcessors(this TracerProviderBuilder builder)
    {
        return builder
            .AddProcessor(new TransactionIdProcessor());
    }

    public static TracerProviderBuilder AddElasticExporter(this TracerProviderBuilder builder) => AddElasticExporter(builder, null, null);

    /// <summary>
    /// Adds the OTLP exporter to the tracer, configured to send data to an Elastic APM backend.
    /// </summary>
    /// <remarks>
    /// 
    /// </remarks>
    /// <param name="builder"></param>
    /// <param name="configure"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    public static TracerProviderBuilder AddElasticExporter(this TracerProviderBuilder builder, Action<OtlpExporterOptions>? configure, string? name)
    {
        // TODO - We need a clean way to test this behaviour

        const string tracesPath = "/v1/traces";

        // If the consumer provides an action to configure the exporter, we use that directly.
        if (configure is not null)
            return name is not null ? builder.AddOtlpExporter(name, configure): builder.AddOtlpExporter(configure);

        // Access OpenTelemetry environment variables used to configure the OTLP exporer.
        // When these are present, we won't attempt to fallback to elastic environment variables.
        var otlpExporterTracesEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT");
        var otlpExporterEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        var otlpExporterHeaders = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS");

        Uri? endpoint = null;

        if (string.IsNullOrEmpty(otlpExporterTracesEndpoint) && string.IsNullOrEmpty(otlpExporterEndpoint))
        {
            var elasticApmServerUrl = Environment.GetEnvironmentVariable("ELASTIC_APM_SERVER_URL");

            if (!string.IsNullOrEmpty(elasticApmServerUrl))
            {
                endpoint = new Uri($"{elasticApmServerUrl}{tracesPath}");
            }
        }
        else if (string.IsNullOrEmpty(otlpExporterTracesEndpoint))
        {
            endpoint = new Uri($"{otlpExporterEndpoint}{tracesPath}");
        }

        var headersIsNullOrEmpty = string.IsNullOrEmpty(otlpExporterHeaders);

        if (headersIsNullOrEmpty || !otlpExporterHeaders!.Contains("Authorization="))
        {
            var elasticApmSecretToken = Environment.GetEnvironmentVariable("ELASTIC_APM_SECRET_TOKEN");

            if (!string.IsNullOrEmpty(elasticApmSecretToken))
            {
                if (headersIsNullOrEmpty)
                    otlpExporterHeaders = $"Authorization=Bearer {elasticApmSecretToken}";
                else
                    otlpExporterHeaders += otlpExporterHeaders + $",Authorization=Bearer {elasticApmSecretToken}";
            }
        }

        // TODO - We can't implement this right now as the Otel SDK will also try to add this header and this causes an
        // exception if we set this first. We will open an issue to discuss that behaviour in their repo.

        //if (!string.IsNullOrEmpty(otlpExporterHeaders))
        //    otlpExporterHeaders += ",User-Agent=TEST-AGENT";
        //else
        //    otlpExporterHeaders = "User-Agent=TEST-AGENT";

        if (endpoint is not null || otlpExporterHeaders is not null)
        {
            configure = _ => { };

            if (endpoint is not null)
                configure += options => options.Endpoint = endpoint;

            if (otlpExporterHeaders is not null)
                configure += options => options.Headers = otlpExporterHeaders;

            return builder.AddOtlpExporter(configure);
        }

        return builder.AddOtlpExporter();
    }
}
