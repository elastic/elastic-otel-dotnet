// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Elastic.OpenTelemetry.Core;
using Elastic.OpenTelemetry.Diagnostics;
using Elastic.OpenTelemetry.Instrumentation;
using Elastic.OpenTelemetry.SemanticConventions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace OpenTelemetry.Resources;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides extension methods on the <see cref="ResourceBuilder"/> used to register
/// the Elastic Distribution of OpenTelemetry (EDOT) defaults.
/// </summary>
internal static class ResourceBuilderExtensions
{
	private static readonly string InstanceId = Guid.NewGuid().ToString();

#pragma warning disable IDE0028 // Simplify collection initialization
	private static readonly ConditionalWeakTable<ResourceBuilder, string> ConfiguredBuilders = new();
#pragma warning restore IDE0028 // Simplify collection initialization

#if !NET
	private static readonly Lock Lock = new();
#endif

	public static ResourceBuilder WithElasticDefaults(this ResourceBuilder builder) =>
		WithElasticDefaults(builder, NullLogger.Instance);

	[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "The call to `AssemblyScanning.AddInstrumentationViaReflection` " +
		"is guarded by a RuntimeFeature.IsDynamicCodeSupported` check and therefore this method is safe to call in AoT scenarios.")]
	public static ResourceBuilder WithElasticDefaults(this ResourceBuilder builder, ILogger logger)
	{
		var defaultServiceName = "unknown_service";

		if (ConfiguredBuilders.TryGetValue(builder, out var builderIdentifier))
		{
			logger.LogResourceBuilderAlreadyConfigured($"{builderIdentifier}:{builder.GetHashCode()}");
			return builder;
		}

		builderIdentifier = Guid.NewGuid().ToString();

#if NET
		ConfiguredBuilders.TryAdd(builder, builderIdentifier);
#else
		using (var scope = Lock.EnterScope())
		{
			ConfiguredBuilders.Add(builder, builderIdentifier);
		}
#endif

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
			.AddAttributes(new Dictionary<string, object>
			{
				{ ResourceSemanticConventions.AttributeServiceName, defaultServiceName },
				{ ResourceSemanticConventions.AttributeServiceInstanceId, InstanceId },
				{ ResourceSemanticConventions.AttributeTelemetryDistroName, "elastic" },
				{ ResourceSemanticConventions.AttributeTelemetryDistroVersion, VersionHelper.InformationalVersion }
			});

#if NET
		if (RuntimeFeature.IsDynamicCodeSupported)
#endif
		{
			// Currently, this adds just the HostDetector is the DLL is present. This ensures that this method can be called
			// by auto-instrumentation which won't ship with that DLL. For the NuGet package, we depend on it so it will always
			// be present.
			SignalBuilder.AddInstrumentationViaReflection(builder, logger, ContribResourceDetectors.GetContribResourceDetectors());
		}

		logger.LogResourceBuilderConfigured($"{builderIdentifier}:{builder.GetHashCode()}");

		return builder;
	}
}
