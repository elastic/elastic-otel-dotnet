// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.Core.Diagnostics;

internal static class LoadedAssemblyLogHelper
{
	// Assemblies with no independent diagnostic value — their identity is already captured by
	// the .NET runtime version string, or they are infrastructure shims.
	private static readonly HashSet<string> SkipByName = new(StringComparer.OrdinalIgnoreCase)
		{ "mscorlib", "netstandard", "System.Private.CoreLib", "dotnet" };

	// Key: "{name}|{assemblyVersion}" — includes version so that different versions of the same
	// library loaded into separate AssemblyLoadContexts are each captured.
	private static readonly ConcurrentDictionary<string, byte> Logged = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Subscribes to <see cref="AppDomain.AssemblyLoad"/> and logs assemblies as they load.
	/// Also scans assemblies already loaded at call time. Subscription is established first
	/// so no assembly loaded during the initial scan is missed.
	/// </summary>
	internal static void Subscribe(ILogger logger)
	{
		AppDomain.CurrentDomain.AssemblyLoad += (_, args) => TryLog(logger, args.LoadedAssembly);

		foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
			TryLog(logger, assembly);
	}

	[RequiresAssemblyFiles("Calls System.Reflection.Assembly.Location")]
	private static void TryLog(ILogger logger, Assembly assembly)
	{
		if (!logger.IsEnabled(LogLevel.Debug))
			return;

		try
		{
			var assemblyName = assembly.GetName();
			var name = assemblyName.Name;
			if (string.IsNullOrEmpty(name) || SkipByName.Contains(name))
				return;

			// Key on name + assembly version: the same library name can appear multiple times in a
			// process when different versions are loaded into separate AssemblyLoadContexts. Each
			// distinct binding version is worth logging — version conflicts are a frequent support cause.
			// Keying on assembly version (not informational version) matches the CLR binding identity.
			var assemblyVersion = assemblyName.Version?.ToString() ?? "unknown";
			if (!Logged.TryAdd($"{name}|{assemblyVersion}", 0))
				return;

			// AssemblyInformationalVersion is the most precise — it carries the full semantic version
			// including pre-release labels and git commit SHA for official packages.
			// AssemblyVersion is logged separately because it is often locked to a major version for
			// binary compatibility and may differ significantly from the actual release version.
			var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
			var location = assembly.Location;

			var tokenBytes = assemblyName.GetPublicKeyToken();
			var publicKeyToken = tokenBytes is { Length: > 0 }
				? BitConverter.ToString(tokenBytes).Replace("-", "").ToLowerInvariant()
				: "null";

			if (informationalVersion != null)
			{
				logger.LogDebug(
					"Assembly loaded: {AssemblyName}, AssemblyVersion={AssemblyVersion}, InformationalVersion={InformationalVersion}, PublicKeyToken={PublicKeyToken}, Location={Location}",
					name, assemblyVersion, informationalVersion, publicKeyToken, location);
			}
			else
			{
				logger.LogDebug(
					"Assembly loaded: {AssemblyName}, AssemblyVersion={AssemblyVersion}, PublicKeyToken={PublicKeyToken}, Location={Location}",
					name, assemblyVersion, publicKeyToken, location);
			}
		}
		catch
		{
			// Never let assembly logging disturb the application.
		}
	}
}
