// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Runtime.InteropServices;

namespace Elastic.OpenTelemetry.IntegrationTests.Helpers;

/// <summary>
/// A <see cref="FactAttribute"/> that skips on non-Windows platforms.
/// Used for .NET Framework (net462) tests which require Windows.
/// </summary>
public sealed class WindowsOnlyFact : FactAttribute
{
	public WindowsOnlyFact()
	{
		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			Skip = ".NET Framework tests require Windows";
	}
}
