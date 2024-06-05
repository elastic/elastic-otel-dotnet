// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// Modified from https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/44c8576ef1290d9fbb6fbbdc973ae1b344afb4c2/src/OpenTelemetry.ResourceDetectors.Host/HostDetector.cs
// As the host.id is support not yet released, we are using the code from the contrib project directly for now.

// TODO - Switch to the contrib package once the features we need are released.

using System.Diagnostics;
using System.Text;
using Elastic.OpenTelemetry.SemanticConventions;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Resources;
#if NETFRAMEWORK || NET6_0_OR_GREATER
using Microsoft.Win32;
#endif

namespace OpenTelemetry.ResourceDetectors.Host;

/// <summary>
/// Host detector.
/// </summary>
internal sealed class HostDetector : IResourceDetector
{
	private const string ETCMACHINEID = "/etc/machine-id";
	private const string ETCVARDBUSMACHINEID = "/var/lib/dbus/machine-id";

	private readonly PlatformID _platformId;
	private readonly Func<IEnumerable<string>> _getFilePaths;
	private readonly Func<string?> _getMacOsMachineId;
	private readonly Func<string?> _getWindowsMachineId;

	private readonly ILogger _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="HostDetector"/> class.
	/// </summary>
	internal HostDetector(ILogger logger)
	{
		_platformId = Environment.OSVersion.Platform;
		_getFilePaths = GetFilePaths;
		_getMacOsMachineId = GetMachineIdMacOs;
		_getWindowsMachineId = GetMachineIdWindows;
		_logger = logger;
	}

	/// <summary>
	/// Detects the resource attributes from host.
	/// </summary>
	/// <returns>Resource with key-value pairs of resource attributes.</returns>
	public Resource Detect()
	{
		try
		{
			var attributes = new List<KeyValuePair<string, object>>(2)
			{
				new(ResourceSemanticConventions.AttributeHostName, Environment.MachineName),
			};
			var machineId = GetMachineId();

			if (machineId != null && !string.IsNullOrEmpty(machineId))
			{
				attributes.Add(new(ResourceSemanticConventions.AttributeHostId, machineId));
			}

			return new Resource(attributes);
		}
		catch (InvalidOperationException ex)
		{
			// Handling InvalidOperationException due to https://learn.microsoft.com/en-us/dotnet/api/system.environment.machinename#exceptions
			_logger.LogError("Failed to detect host resource due to {Exception}", ex);
		}

		return Resource.Empty;
	}

	internal static string? ParseMacOsOutput(string? output)
	{
		if (output == null || string.IsNullOrEmpty(output))
		{
			return null;
		}

		var lines = output.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);

		foreach (var line in lines)
		{
#if NETFRAMEWORK
            if (line.IndexOf("IOPlatformUUID", StringComparison.OrdinalIgnoreCase) >= 0)
#else
			if (line.Contains("IOPlatformUUID", StringComparison.OrdinalIgnoreCase))
#endif
			{
				var parts = line.Split('"');

				if (parts.Length > 3)
				{
					return parts[3];
				}
			}
		}

		return null;
	}

	private static IEnumerable<string> GetFilePaths()
	{
		yield return ETCMACHINEID;
		yield return ETCVARDBUSMACHINEID;
	}

	private string? GetMachineIdMacOs()
	{
		try
		{
			var startInfo = new ProcessStartInfo
			{
				FileName = "sh",
				Arguments = "ioreg -rd1 -c IOPlatformExpertDevice",
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
			};

			var sb = new StringBuilder();
			using var process = Process.Start(startInfo);
			process?.WaitForExit();
			sb.Append(process?.StandardOutput.ReadToEnd());
			return sb.ToString();
		}
		catch (Exception ex)
		{
			_logger.LogError("Failed to get machine ID on MacOS due to {Exception}", ex);
		}

		return null;
	}

#pragma warning disable CA1416
	// stylecop wants this protected by System.OperatingSystem.IsWindows
	// this type only exists in .NET 5+
	private string? GetMachineIdWindows()
	{
#if NETFRAMEWORK || NET6_0_OR_GREATER
		try
		{
			using var subKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography", false);
			return subKey?.GetValue("MachineGuid") as string ?? null;
		}
		catch (Exception ex)
		{
			_logger.LogError("Failed to get machine ID on Windows due to {Exception}", ex);
		}
#endif

		return null;
	}
#pragma warning restore CA1416

	private string? GetMachineId() => _platformId switch
	{
		PlatformID.Unix => GetMachineIdLinux(),
		PlatformID.MacOSX => ParseMacOsOutput(_getMacOsMachineId()),
		PlatformID.Win32NT => _getWindowsMachineId(),
		_ => null,
	};

	private string? GetMachineIdLinux()
	{
		var paths = _getFilePaths();

		foreach (var path in paths)
		{
			if (File.Exists(path))
			{
				try
				{
					return File.ReadAllText(path).Trim();
				}
				catch (Exception ex)
				{
					_logger.LogError("Failed to get machine ID on Linux due to {Exception}", ex);
				}
			}
		}

		return null;
	}
}
