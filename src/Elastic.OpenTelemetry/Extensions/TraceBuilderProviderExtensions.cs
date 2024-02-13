// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information
using Elastic.OpenTelemetry.Processors;
using OpenTelemetry.Exporter;
using OpenTelemetry.Trace;
using OpenTelemetry;
using System.Diagnostics;
using Elastic.OpenTelemetry.Diagnostics;

using static Elastic.OpenTelemetry.Diagnostics.ElasticOpenTelemetryDiagnostics;

namespace Elastic.OpenTelemetry.Extensions;

internal readonly record struct AddProcessorEvent(Type ProcessorType, Type BuilderType);

internal readonly record struct AddSourceEvent(string ActivitySourceName, Type BuilderType);

/// <summary> Provides Elastic APM extensions to <see cref="TracerProviderBuilder"/> </summary>
public static class TraceBuilderProviderExtensions
{
	//TODO binder source generator on Build() to make it automatic?
	/// <summary> Include Elastic APM Trace Processors to ensure data is enriched and extended.</summary>
	public static TracerProviderBuilder AddElasticProcessors(this TracerProviderBuilder builder) =>
		builder.LogAndAddProcessor(new TransactionIdProcessor());

	internal static TracerProviderBuilder LogAndAddProcessor(this TracerProviderBuilder builder, BaseProcessor<Activity> processor)
	{
		Log(ProcessorAddedEvent, () => new DiagnosticEvent<AddProcessorEvent>(new(processor.GetType(), builder.GetType())));
		return builder.AddProcessor(processor);
	}

	internal static TracerProviderBuilder LogAndAddSource(this TracerProviderBuilder builder, string sourceName)
	{
		Log(SourceAddedEvent, () => new DiagnosticEvent<AddSourceEvent>(new(sourceName, builder.GetType())));
		return builder.AddSource(sourceName);
	}

	/// <summary>
	/// Adds the OTLP exporter to the tracer, configured to send data to an Elastic APM backend.
	/// </summary>
	public static TracerProviderBuilder AddElasticOtlpExporter(this TracerProviderBuilder builder) =>
		AddElasticOtlpExporter(builder, null, null);

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
    public static TracerProviderBuilder AddElasticOtlpExporter(this TracerProviderBuilder builder, Action<OtlpExporterOptions>? configure, string? name)
    {
        // TODO - We need a clean way to test this behaviour
		// TODO - Logging

        const string tracesPath = "/v1/traces";

        // If the consumer provides an action to configure the exporter, we use that directly.
        // TODO - This does mean that if we end up relying on the HttpClientFactory action (see below), we cannot apply the
        // User-Agent header here unless we also update this action. That's okay unless the consumer provides their own action.
        if (configure is not null)
            return name is not null ? builder.AddOtlpExporter(name, configure): builder.AddOtlpExporter(configure);

        // Access OpenTelemetry environment variables used to configure the OTLP exporter.
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
        // exception if we set this first. https://github.com/open-telemetry/opentelemetry-dotnet/issues/5146 has been opened to discuss
        // that behaviour in the SDK. Until then, a partial workaround has been added below.

        //if (!string.IsNullOrEmpty(otlpExporterHeaders))
        //    otlpExporterHeaders += ",User-Agent=TEST-AGENT";
        //else
        //    otlpExporterHeaders = "User-Agent=TEST-AGENT";

        if (endpoint is not null || otlpExporterHeaders is not null)
        {
            // TODO - This is only to demonstrate the workaround (for now).
            // If we end up relying on this, we need to set this outside of this if block so it applies even
            // when we have no endpoint or headers to add.
#pragma warning disable IDE0053 // Use expression body for lambda expression
            configure = o =>
            {
                // NOTE: this only applies if we also force the protocol to HTTP protobuf (see below) which we avoid for now.
                o.HttpClientFactory = () => new HttpClient(new UserAgentMessageHandler(new SocketsHttpHandler()))
                {
                    Timeout = TimeSpan.FromMilliseconds(o.TimeoutMilliseconds)
                };

                // We prefer gRPC for performance
                // TODO - Investigate if multiple gRPC metadata entries for `User-Agent` apply when sent over the wire.
                //o.Protocol = OtlpExportProtocol.HttpProtobuf;
            };
#pragma warning restore IDE0053 // Use expression body for lambda expression

            if (endpoint is not null)
                configure += options => options.Endpoint = endpoint;

            if (otlpExporterHeaders is not null)
                configure += options => options.Headers = otlpExporterHeaders;

#if DEBUG
			configure += options => options.ExportProcessorType = ExportProcessorType.Simple;
#endif

			return builder.AddOtlpExporter(configure);
        }

        return builder.AddOtlpExporter();
    }
}

internal sealed class UserAgentMessageHandler(HttpMessageHandler handler) : DelegatingHandler(handler)
{
    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        UpdateUserAgent(request);
        return base.Send(request, cancellationToken);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        UpdateUserAgent(request);
        return base.SendAsync(request, cancellationToken);
    }

    private static void UpdateUserAgent(HttpRequestMessage request)
    {
        var headers = request.Headers.UserAgent;
        var firstProduct = headers.FirstOrDefault();

        headers.Clear();
        headers.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("Elastic-Otel-Distro-Dotnet", Agent.InformationalVersion));

        if (firstProduct is not null)
            headers.Add(firstProduct);
    }
}
