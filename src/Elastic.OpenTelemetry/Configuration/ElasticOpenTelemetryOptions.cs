// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Configuration;
using Microsoft.Extensions.Logging;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Elastic.OpenTelemetry;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Defines options which can be used to finely-tune the behaviour of the Elastic
/// distribution of OpenTelemetry.
/// </summary>
public class ElasticOpenTelemetryOptions
{
	/// <summary>
	/// The output directory where the Elastic Distribution of OpenTelemetry .NET will write log files.
	/// </summary>
	/// <remarks>
	/// When configured, a file log will be created in this directory with the name
	/// <c>{ProcessName}_{UtcUnixTimeMilliseconds}_{ProcessId}.instrumentation.log</c>.
	/// This log file includes log messages from the OpenTelemetry SDK and the Elastic distribution.
	/// </remarks>
	public string? LogDirectory { get; init; }

	/// <summary>
	/// The log level to use when writing log files.
	/// </summary>
	/// <remarks>
	/// Valid values are:
	/// <list type="bullet">
	/// <item><term>None</term><description>Disables logging.</description></item>
	/// <item><term>Critical</term><description>Failures that require immediate attention.</description></item>
	/// <item><term>Error</term><description>Errors and exceptions that cannot be handled.</description></item>
	/// <item><term>Warning</term><description>Abnormal or unexpected events.</description></item>
	/// <item><term>Information</term><description>General information about the distribution and OpenTelemetry SDK.</description></item>
	/// <item><term>Debug</term><description>Rich debugging and development.</description></item>
	/// <item><term>Trace</term><description>Contain the most detailed messages.</description></item>
	/// </list>
	/// </remarks>
	public LogLevel? LogLevel { get; init; }

	/// <summary>
	/// Control the targets that the Elastic Distribution of OpenTelemetry .NET will log to.
	/// </summary>
	public LogTargets? LogTargets { get; init; }

	/// <summary>
	/// Stops Elastic Distribution of OpenTelemetry .NET from registering OLTP exporters, useful for testing scenarios.
	/// </summary>
	public bool? SkipOtlpExporter { get; init; }

	/// <summary>
	/// An additional <see cref="ILogger"/> to which logs will be written.
	/// </summary>
	public ILogger? AdditionalLogger { get; init; }

	/// <summary>
	/// An <see cref="ILoggerFactory"/> that can be used to create an additional <see cref="ILogger"/>
	/// to which logs will be written.
	/// </summary>
	public ILoggerFactory? AdditionalLoggerFactory { get; init; }
}
