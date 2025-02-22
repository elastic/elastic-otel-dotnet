// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Core;
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
	private readonly BootstrapInfo _bootstrapInfo;
	private readonly ElasticOpenTelemetryComponents? _components;

	/// <inheritdoc cref="AutoInstrumentationPlugin"/>
	public AutoInstrumentationPlugin()
	{
		SetError();

		_bootstrapInfo = GetBootstrapInfo(out var components);

		if (!_bootstrapInfo.Succeeded)
		{
			var errorMessage = $"Unable to bootstrap EDOT .NET due to {_bootstrapInfo.Exception!.Message}";

			Console.Error.WriteLine(errorMessage);

			try // Attempt to log the bootstrap failure to a file
			{
				var options = new CompositeElasticOpenTelemetryOptions();

				var directory = options.LogDirectory;

				if (string.IsNullOrEmpty(directory))
					return;

				var process = Process.GetCurrentProcess();
				var logFileName = $"{process.ProcessName}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{process.Id}.auto_instrumentation.log";

				Directory.CreateDirectory(directory);

				using var streamWriter = File.CreateText(Path.Combine(directory, logFileName));

				streamWriter.WriteLine(errorMessage);
			}
			catch
			{
				// Intended empty catch here. We don't want to crash the application.
			}

			return;
		}

		_components = components!;
	}

	// Used for testing
	internal virtual BootstrapInfo GetBootstrapInfo(out ElasticOpenTelemetryComponents? components) =>
		ElasticOpenTelemetry.TryBootstrap(SdkActivationMethod.AutoInstrumentation, out components);

	// Used for testing
	internal virtual void SetError() { }

	/// <summary>
	/// To configure tracing SDK before Auto Instrumentation configured SDK.
	/// </summary>
	public TracerProviderBuilder BeforeConfigureTracerProvider(TracerProviderBuilder builder) =>
		!_bootstrapInfo.Succeeded || _components is null
			? builder
			: builder.UseAutoInstrumentationElasticDefaults(_components);

	/// <summary>
	/// To configure metrics SDK before Auto Instrumentation configured SDK.
	/// /// </summary>
	public MeterProviderBuilder BeforeConfigureMeterProvider(MeterProviderBuilder builder) =>
		!_bootstrapInfo.Succeeded || _components is null
			? builder
			: builder.UseElasticDefaults(_components);

	/// <summary>
	/// To configure logs SDK (the method name is the same as for other logs options).
	/// </summary>
	public void ConfigureLogsOptions(OpenTelemetryLoggerOptions options)
	{
		if (_bootstrapInfo.Succeeded && _components is not null)
			options.UseElasticDefaults(_components.Logger);
	}

	/// <summary>
	/// To configure Resource.
	/// </summary>
	public ResourceBuilder ConfigureResource(ResourceBuilder builder) =>
		!_bootstrapInfo.Succeeded || _components is null
			? builder
			: builder.AddElasticDistroAttributes();
}
