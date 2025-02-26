// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using Elastic.OpenTelemetry.Core;
using Elastic.OpenTelemetry.SemanticConventions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry.Resources;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace OpenTelemetry;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods for <see cref="ResourceBuilder"/>.
/// </summary>
public static class ResourceBuilderExtensions
{
	private static readonly string InstanceId = Guid.NewGuid().ToString();

	/// <summary>
	/// For advanced scenarios, where the all Elastic defaults are not disabled (essentially using the "vanilla" OpenTelemetry SDK),
	/// this method can be used to add Elastic resource defaults to the <see cref="ResourceBuilder"/>.
	/// </summary>
	/// <remarks>
	/// After clearing the <see cref="ResourceBuilder"/> the following are added in order:
	/// <list type="bullet">
	/// <item>A default, fallback <c>service.name</c>.</item>
	/// <item>A default, unqiue <c>service.instance.id</c>.</item>
	/// <item>The telemetry SDK attributes via <c>AddTelemetrySdk()</c>.</item>
	/// <item>The Elastic telemetry distro attributes.</item>
	/// <item>Adds resource attributes parsed from OTEL_RESOURCE_ATTRIBUTES, OTEL_SERVICE_NAME environment variables
	/// via <c>AddEnvironmentVariableDetector()</c>.</item>
	/// <item>Host attributes, <c>host.name</c> and <c>host.id</c> (on supported targets).</item>
	/// </list>
	/// <para>These mostly mirror what the vanilla SDK does, but allow us to ensure that certain resources attributes that the
	/// Elastic APM backend requires to drive the UIs are present in some form. Any of these may be overridden by further
	/// resource configuration.</para>
	/// </remarks>
	/// <param name="builder">A <see cref="ResourceBuilder"/> that will be configured with Elastic defaults.</param>
	/// <param name="logger">Optionally provide a logger to log to</param>
	/// <returns>The <see cref="ResourceBuilder"/> for chaining calls.</returns>
	public static ResourceBuilder WithElasticDefaults(this ResourceBuilder builder, ILogger? logger = null)
	{
		// ReSharper disable once RedundantAssignment
#pragma warning disable IDE0059 // Unnecessary assignment of a value
		logger ??= NullLogger.Instance;
#pragma warning restore IDE0059 // Unnecessary assignment of a value
		var defaultServiceName = "unknown_service";

		try
		{
			var processName = Process.GetCurrentProcess().ProcessName;
			if (!string.IsNullOrWhiteSpace(processName))
				defaultServiceName = $"{defaultServiceName}:{processName}";
		}
		catch
		{
			// GetCurrentProcess can throw PlatformNotSupportedException
		}

		builder
			.Clear()
			.AddAttributes(new Dictionary<string, object>
			{
				{ ResourceSemanticConventions.AttributeServiceName, defaultServiceName },
				{ ResourceSemanticConventions.AttributeServiceInstanceId, InstanceId }
			})
			.AddTelemetrySdk()
			.AddElasticDistroAttributes()
			.AddEnvironmentVariableDetector()
			.AddHostDetector();

		return builder;
	}

	internal static ResourceBuilder AddElasticDistroAttributes(this ResourceBuilder builder) =>
		builder.AddAttributes(new Dictionary<string, object>
		{
			{ ResourceSemanticConventions.AttributeTelemetryDistroName, "elastic" },
			{ ResourceSemanticConventions.AttributeTelemetryDistroVersion, VersionHelper.InformationalVersion }
		});
}
