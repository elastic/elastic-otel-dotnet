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
	// NOTE: We don't use nameof + string interpolation for the bootstrap log messages.
	// This avoids cluttering the code with a check to see if bootstrap logging is enabled before the log message.
	// This class will change rarely so the risk of renaming issues is low.

	private static readonly ElasticOpenTelemetryComponents Components;

	static AutoInstrumentationPlugin()
	{
		BootstrapLogger.LogWithStackTrace("AutoInstrumentationPlugin: Initializing via static constructor");
		Components = ElasticOpenTelemetry.Bootstrap(SdkActivationMethod.AutoInstrumentation, new(), null);
	}

	/// <summary>
	/// Configure Resource Builder for Logs, Metrics and Traces
	/// </summary>
	/// <param name="builder"><see cref="ResourceBuilder"/> to configure</param>
	/// <returns>Returns <see cref="ResourceBuilder"/> for chaining.</returns>
	public ResourceBuilder ConfigureResource(ResourceBuilder builder)
	{
		BootstrapLogger.Log("AutoInstrumentationPlugin: ConfigureResource invoked");
		builder.WithElasticDefaultsCore(Components, null, null);
		return builder;
	}

	/// <summary>
	/// To configure tracing SDK before Auto Instrumentation configured SDK.
	/// </summary>
	public TracerProviderBuilder BeforeConfigureTracerProvider(TracerProviderBuilder builder)
	{
		BootstrapLogger.Log("AutoInstrumentationPlugin: BeforeConfigureTracerProvider invoked");
		var logger = Components.Logger;

		try
		{
			logger.LogInformation("Configuring Elastic Distribution of OpenTelemetry .NET defaults for tracing auto-instrumentation.");

			ElasticTracerProviderBuilderExtensions.AddActivitySourceWithLogging(builder, logger, "Elastic.Transport", "<n/a>");
			ElasticTracerProviderBuilderExtensions.AddElasticProcessorsCore(builder, null, Components, null);

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
	public void ConfigureTracesOptions(OtlpExporterOptions options)
	{
		BootstrapLogger.Log("AutoInstrumentationPlugin: ConfigureTracesOptions(OtlpExporterOptions) invoked");
		ConfigureOtlpExporter(options, "traces");
	}

	/// <summary>
	/// Configure metrics OTLP exporter options
	/// </summary>
	/// <param name="options">Otlp options</param>
	public void ConfigureMetricsOptions(OtlpExporterOptions options)
	{
		BootstrapLogger.Log("AutoInstrumentationPlugin: ConfigureMetricsOptions(OtlpExporterOptions) invoked");
		ConfigureOtlpExporter(options, "metrics");
	}

	/// <summary>
	/// Configure metrics OTLP exporter options
	/// </summary>
	/// <param name="options">Otlp options</param>
	public void ConfigureMetricsOptions(MetricReaderOptions options)
	{
		BootstrapLogger.Log("AutoInstrumentationPlugin: ConfigureMetricsOptions(MetricReaderOptions) invoked");
		var logger = Components.Logger;
		options.TemporalityPreference = MetricReaderTemporalityPreference.Delta;
		logger.LogInformation("Configured Elastic Distribution of OpenTelemetry .NET defaults for logging auto-instrumentation.");
	}

	/// <summary>
	/// Configure logging OTLP exporter options.
	/// </summary>
	/// <param name="options">Otlp options.</param>
	public void ConfigureLogsOptions(OtlpExporterOptions options)
	{
		BootstrapLogger.Log("AutoInstrumentationPlugin: ConfigureLogsOptions(OtlpExporterOptions) invoked");
		ConfigureOtlpExporter(options, "logs");
	}

	/// <summary>
	/// To configure logs SDK (the method name is the same as for other logs options).
	/// </summary>
	public void ConfigureLogsOptions(OpenTelemetryLoggerOptions options)
	{
		BootstrapLogger.Log("AutoInstrumentationPlugin: ConfigureLogsOptions(OpenTelemetryLoggerOptions) invoked");
		options.WithElasticDefaults(Components.Logger);
	}

	private static void ConfigureOtlpExporter(OtlpExporterOptions options, string signal)
	{
		options.ConfigureElasticUserAgent();
		Components.Logger.LogConfiguredOtlpExporterOptions(signal);
	}
}
