// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Core;
using Elastic.OpenTelemetry.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
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
	/// To configure tracing SDK before Auto Instrumentation configured SDK.
	/// </summary>
	public TracerProviderBuilder BeforeConfigureTracerProvider(TracerProviderBuilder builder)
	{
		var logger = _components.Logger;

		try
		{
			builder.ConfigureResource(r => r.WithElasticDefaults(_components, null));

			CoreTracerProvderBuilderExtensions.AddActivitySourceWithLogging(builder, logger, "Elastic.Transport", "<n/a>");
			CoreTracerProvderBuilderExtensions.AddElasticProcessorsCore(builder, null, _components, null);

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
	/// To configure metrics SDK before Auto Instrumentation configured SDK.
	/// /// </summary>
	public MeterProviderBuilder BeforeConfigureMeterProvider(MeterProviderBuilder builder)
	{
		var logger = _components.Logger;

		try
		{
			builder.ConfigureResource(r => r.WithElasticDefaults(_components, null));

			logger.LogConfiguredSignalProvider(nameof(Signals.Metrics), nameof(MeterProviderBuilder), "<n/a>");

			return builder;
		}
		catch (Exception ex)
		{
			logger.LogError(new EventId(521, "AutoInstrumentationTracerFailure"), ex,
				"Failed to register EDOT defaults for metrics auto-instrumentation to the MeterProviderBuilder.");
		}

		return builder;
	}

	/// <summary>
	/// To configure logs SDK (the method name is the same as for other logs options).
	/// </summary>
	public void ConfigureLogsOptions(OpenTelemetryLoggerOptions options) =>
		options.WithElasticDefaults(_components.Logger);

	/// <summary>
	/// To configure Resource.
	/// </summary>
	public ResourceBuilder ConfigureResource(ResourceBuilder builder) =>
		builder.WithElasticDefaults(_components, null);
}
