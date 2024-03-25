// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information
using System.Reflection;

namespace Elastic.OpenTelemetry;

internal static class VersionHelper
{
	static VersionHelper()
	{
		var assemblyInformationalVersion = typeof(VersionHelper).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
		InformationalVersion = ParseAssemblyInformationalVersion(assemblyInformationalVersion);
	}

	internal static string InformationalVersion { get; }

	private static string ParseAssemblyInformationalVersion(string? informationalVersion)
	{
		if (string.IsNullOrWhiteSpace(informationalVersion))
			informationalVersion = "1.0.0";

		/*
		 * InformationalVersion will be in the following format:
		 *   {majorVersion}.{minorVersion}.{patchVersion}.{pre-release label}.{pre-release version}.{gitHeight}+{Git SHA of current commit}
		 * Ex: 1.5.0-alpha.1.40+807f703e1b4d9874a92bd86d9f2d4ebe5b5d52e4
		 * The following parts are optional: pre-release label, pre-release version, git height, Git SHA of current commit
		 */

		var indexOfPlusSign = informationalVersion.IndexOf('+');
		return indexOfPlusSign > 0
			? informationalVersion[..indexOfPlusSign]
			: informationalVersion;
	}
}
