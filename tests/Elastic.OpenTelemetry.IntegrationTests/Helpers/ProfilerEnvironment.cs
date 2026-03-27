// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Runtime.InteropServices;

namespace Elastic.OpenTelemetry.IntegrationTests.Helpers;

/// <summary>
/// Generates the profiler environment variables needed to run an application
/// under the OpenTelemetry .NET auto-instrumentation with the EDOT plugin.
/// </summary>
internal static class ProfilerEnvironment
{
	private const string ProfilerClsid = "{918728DD-259F-4A6A-AC2B-B85E1B658318}";

	private const string PluginTypeName =
		"Elastic.OpenTelemetry.AutoInstrumentationPlugin, Elastic.OpenTelemetry.AutoInstrumentation";

	/// <summary>
	/// Generates env vars for .NET (net8.0+) auto-instrumentation via CoreCLR profiler.
	/// </summary>
	public static Dictionary<string, string> ForCoreCLR(string installDir) =>
		new()
		{
			["CORECLR_ENABLE_PROFILING"] = "1",
			["CORECLR_PROFILER"] = ProfilerClsid,
			["CORECLR_PROFILER_PATH"] = GetCoreCLRProfilerPath(installDir),
			["OTEL_DOTNET_AUTO_HOME"] = installDir,
			["OTEL_DOTNET_AUTO_INSTALL_DIR"] = installDir,
			["OTEL_DOTNET_AUTO_PLUGINS"] = PluginTypeName,
			["DOTNET_ADDITIONAL_DEPS"] = Path.Combine(installDir, "AdditionalDeps"),
			["DOTNET_SHARED_STORE"] = Path.Combine(installDir, "store"),
			["DOTNET_STARTUP_HOOKS"] = Path.Combine(installDir, "net",
				"OpenTelemetry.AutoInstrumentation.StartupHook.dll"),
		};

	/// <summary>
	/// Generates env vars for .NET Framework (net462) auto-instrumentation via COM profiler.
	/// Windows only — .NET Framework does not run on other platforms.
	/// </summary>
	public static Dictionary<string, string> ForNetFramework(string installDir) =>
		new()
		{
			["COR_ENABLE_PROFILING"] = "1",
			["COR_PROFILER"] = ProfilerClsid,
			["COR_PROFILER_PATH_32"] = Path.Combine(installDir, "win-x86",
				"OpenTelemetry.AutoInstrumentation.Native.dll"),
			["COR_PROFILER_PATH_64"] = Path.Combine(installDir, "win-x64",
				"OpenTelemetry.AutoInstrumentation.Native.dll"),
			["OTEL_DOTNET_AUTO_HOME"] = installDir,
			["OTEL_DOTNET_AUTO_INSTALL_DIR"] = installDir,
			["OTEL_DOTNET_AUTO_PLUGINS"] = PluginTypeName,
		};

	private static string GetCoreCLRProfilerPath(string installDir)
	{
		var (subDir, fileName) = RuntimeInformation.ProcessArchitecture switch
		{
			Architecture.X64 when RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
				=> ("win-x64", "OpenTelemetry.AutoInstrumentation.Native.dll"),
			Architecture.X64 when RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
				=> ("linux-x64", "OpenTelemetry.AutoInstrumentation.Native.so"),
			Architecture.Arm64 when RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
				=> ("linux-arm64", "OpenTelemetry.AutoInstrumentation.Native.so"),
			Architecture.X64 when RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
				=> ("osx-x64", "OpenTelemetry.AutoInstrumentation.Native.dylib"),
			Architecture.Arm64 when RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
				=> ("osx-arm64", "OpenTelemetry.AutoInstrumentation.Native.dylib"),
			_ => throw new PlatformNotSupportedException(
				$"Unsupported platform: {RuntimeInformation.OSDescription} {RuntimeInformation.ProcessArchitecture}")
		};

		return Path.Combine(installDir, subDir, fileName);
	}
}
