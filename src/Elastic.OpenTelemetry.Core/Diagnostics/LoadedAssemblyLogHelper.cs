// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.Core.Diagnostics;

internal sealed class LoadedAssemblyLogHelper
{
	private static HashSet<string>? LoggedAssemblies;

	internal static void LogLoadedAssemblies(ILogger logger)
	{
		if (!logger.IsEnabled(LogLevel.Debug))
			return;

		try
		{
			var assemblies = AppDomain.CurrentDomain.GetAssemblies()
			.Where(a => a.GetName().Name?.StartsWith("OpenTelemetry", StringComparison.OrdinalIgnoreCase) == true)
			.OrderBy(a => a.GetName().Name)
			.ToList();

			if (assemblies.Count == 0)
			{
				return;
			}

			LoggedAssemblies ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach (var assembly in assemblies)
			{
				var assemblyName = assembly.GetName();
				var name = assemblyName.Name;

				if (name is null)
					continue;

				var fileVersion = assembly.CustomAttributes.FirstOrDefault(a => a.AttributeType == typeof(System.Reflection.AssemblyFileVersionAttribute))?.ConstructorArguments[0].Value;

				var version = fileVersion ?? assemblyName.Version?.ToString() ?? "unknown";

				if (LoggedAssemblies != null && !LoggedAssemblies.Add(name))
					continue;

				logger.LogDebug("OpenTelemetry assembly found: {AssemblyName} (v{Version})", name, version);
			}
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Unable to log loaded assemblies");
		}
	}
}
