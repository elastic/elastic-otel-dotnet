// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics.Tracing;
using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Extensions;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Elastic.OpenTelemetry.AutoInstrumentationPlugin;

/// <summary>
/// Elastic Distribution for OpenTelemetry .NET plugin for Auto Instrumentation.
/// <para>Ensures all signals are rich enough to report to Elastic</para>
/// </summary>
// ReSharper disable once UnusedType.Global
public class ElasticAutoInstrumentationPlugin
{
	private readonly ILogger _logger;
	private readonly EventListener _eventListener;

	/// <inheritdoc cref="ElasticAutoInstrumentationPlugin"/>
	public ElasticAutoInstrumentationPlugin()
	{
		Console.WriteLine(nameof(ElasticAutoInstrumentationPlugin));
		var options = new ElasticOpenTelemetryBuilderOptions();
		var (eventListener, logger) = ElasticOpenTelemetryBuilder.Bootstrap(options);

		_logger = logger;
		_eventListener = eventListener;
	}

	/// To access TracerProvider right after TracerProviderBuilder.Build() is executed.
	public void TracerProviderInitialized(TracerProvider tracerProvider)
	{
	}

	/// To access MeterProvider right after MeterProviderBuilder.Build() is executed.
	public void MeterProviderInitialized(MeterProvider meterProvider)
	{
	}

	/// To configure tracing SDK before Auto Instrumentation configured SDK
	public TracerProviderBuilder BeforeConfigureTracerProvider(TracerProviderBuilder builder) =>
		builder.UseElasticDefaults(_logger);


	/// To configure tracing SDK after Auto Instrumentation configured SDK
	public TracerProviderBuilder AfterConfigureTracerProvider(TracerProviderBuilder builder) =>
		builder;

	/// To configure metrics SDK before Auto Instrumentation configured SDK
	public MeterProviderBuilder BeforeConfigureMeterProvider(MeterProviderBuilder builder) =>
		builder.UseElasticDefaults(_logger);

	/// To configure metrics SDK after Auto Instrumentation configured SDK
	public MeterProviderBuilder AfterConfigureMeterProvider(MeterProviderBuilder builder) =>
		builder;


	/// To configure logs SDK (the method name is the same as for other logs options)
	public void ConfigureLogsOptions(OpenTelemetryLoggerOptions options) =>
		options.UseElasticDefaults(_logger);

	/// To configure Resource
	public ResourceBuilder ConfigureResource(ResourceBuilder builder) =>
		builder.UseElasticDefaults(_logger);
}
