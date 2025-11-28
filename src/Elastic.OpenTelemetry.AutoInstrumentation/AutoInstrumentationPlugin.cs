// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Core;
using Elastic.OpenTelemetry.Diagnostics;
using Elastic.OpenTelemetry.Exporters;
using Elastic.OpenTelemetry.Resources;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Elastic.OpenTelemetry;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Elastic Distribution of OpenTelemetry .NET plugin for Auto Instrumentation.
/// <para>Ensures all signals are rich enough to report to Elastic.</para>
/// </summary>
public class AutoInstrumentationPlugin
{
	private readonly ElasticOpenTelemetryComponents _components;

	/// <inheritdoc cref="AutoInstrumentationPlugin"/>
	public AutoInstrumentationPlugin() => _components = ElasticOpenTelemetry.Bootstrap(SdkActivationMethod.AutoInstrumentation);

	/// <summary>
	/// Configure Resource Builder for Logs, Metrics and Traces
	/// </summary>
	/// <param name="builder"><see cref="ResourceBuilder"/> to configure</param>
	/// <returns>Returns <see cref="ResourceBuilder"/> for chaining.</returns>
	public ResourceBuilder ConfigureResource(ResourceBuilder builder)
	{
		builder.WithElasticDefaultsCore(_components, null, null);
		return builder;
	}

	/// <summary>
	/// To configure tracing SDK before Auto Instrumentation configured SDK.
	/// </summary>
	public TracerProviderBuilder BeforeConfigureTracerProvider(TracerProviderBuilder builder)
	{
		var logger = _components.Logger;

		try
		{
			logger.LogInformation("Configuring Elastic Distribution of OpenTelemetry .NET defaults for tracing auto-instrumentation.");

			ElasticTracerProviderBuilderExtensions.AddActivitySourceWithLogging(builder, logger, "Elastic.Transport", "<n/a>");
			ElasticTracerProviderBuilderExtensions.AddElasticProcessorsCore(builder, null, _components, null);

			logger.LogConfiguredSignalProvider("Traces", nameof(TracerProviderBuilder), "<n/a>");

			return builder;
		}
		catch (Exception ex)
		{
			logger.LogError(new EventId(520, "AutoInstrumentationTracerFailure"), ex,
				"Failed to register EDOT defaults for tracing auto-instrumentation to the TracerProviderBuilder.");
		}

		return builder;
	}

	/// <summary>
	/// Configure traces OTLP exporter options.
	/// </summary>
	/// <param name="options">Otlp options.</param>
	public void ConfigureTracesOptions(OtlpExporterOptions options) => ConfigureOtlpExporter(options, "traces");

	/// <summary>
	/// Configure metrics OTLP exporter options
	/// </summary>
	/// <param name="options">Otlp options</param>
	public void ConfigureMetricsOptions(OtlpExporterOptions options) => ConfigureOtlpExporter(options, "metrics");

	/// <summary>
	/// Configure metrics OTLP exporter options
	/// </summary>
	/// <param name="options">Otlp options</param>
	public void ConfigureMetricsOptions(MetricReaderOptions options)
	{
		var logger = _components.Logger;
		options.TemporalityPreference = MetricReaderTemporalityPreference.Delta;
		logger.LogInformation("Configured Elastic Distribution of OpenTelemetry .NET defaults for logging auto-instrumentation.");
	}

	/// <summary>
	/// Configure logging OTLP exporter options.
	/// </summary>
	/// <param name="options">Otlp options.</param>
	public void ConfigureLogsOptions(OtlpExporterOptions options) => ConfigureOtlpExporter(options, "logs");

	/// <summary>
	/// To configure logs SDK (the method name is the same as for other logs options).
	/// </summary>
	public void ConfigureLogsOptions(OpenTelemetryLoggerOptions options) => options.WithElasticDefaults(_components.Logger);

	private void ConfigureOtlpExporter(OtlpExporterOptions options, string signal)
	{
		var logger = _components.Logger;
		options.ConfigureElasticUserAgent();
		logger.LogConfiguredOtlpExporterOptions(signal);
	}
}
