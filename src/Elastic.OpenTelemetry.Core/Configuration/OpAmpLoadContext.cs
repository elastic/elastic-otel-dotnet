// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

#if NET8_0_OR_GREATER// && USE_ISOLATED_OPAMP_CLIENT

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.Core.Configuration;

/// <summary>
/// Custom AssemblyLoadContext for isolating Google.Protobuf and OpenTelemetry.OpAmp.Client
/// to prevent version conflicts when the instrumented application brings its own versions
/// of these dependencies.
/// 
/// This is used in profiler-loaded scenarios (AutoInstrumentation) where we have zero control 
/// over the application's dependency versions.
/// 
/// Once an assembly is loaded in this context, its dependencies are resolved within this
/// context as well, preventing version conflicts with the default ALC.
/// 
/// Note: This class is only compiled for .NET 8 and newer frameworks that support AssemblyLoadContext.
/// </summary>
internal sealed class OpAmpLoadContext : AssemblyLoadContext
{
	private readonly ILogger _logger;
	private readonly AssemblyDependencyResolver? _resolver = null;
	private readonly string? _otelInstallationPath = null;

	public OpAmpLoadContext(ILogger logger)
	{
		_logger = logger;

		var otelInstallationPath = Environment.GetEnvironmentVariable("OTEL_DOTNET_AUTO_INSTALL_DIR");

		if (string.IsNullOrEmpty(otelInstallationPath))
		{
			_logger.LogWarning("OpAmpLoadContext: OTEL_DOTNET_AUTO_INSTALL_DIR environment variable is not set. " +
				"Falling back to default assembly resolution which may lead to version conflicts.");

			return;
		}

		_otelInstallationPath = Path.Join(otelInstallationPath, "net");

		// TODO - Check path exists

		_logger.LogDebug("OpAmpLoadContext: Initializing isolated load context for OpenTelemetry OpAmp dependencies from '{OtelInstallationPath}'",
			otelInstallationPath ?? "<null>");

		_resolver = new AssemblyDependencyResolver(otelInstallationPath!);
	}

	[UnconditionalSuppressMessage("AssemblyLoadTrimming", "IL2026: RequiresUnreferencedCode", Justification = "The calls to this ALC will be guarded by a runtime check")]
	protected override Assembly? Load(AssemblyName assemblyName)
	{
		if (_resolver is null)
		{
			// Fallback to default resolution if resolver is not initialized
			_logger.LogDebug("OpAmpLoadContext is not initialized. Falling back to default resolution for '{AssemblyName}'",
				assemblyName.Name);

			return null;
		}

		if (assemblyName.Name is not "Google.Protobuf" and not "OpenTelemetry.OpAmp.Client")
		{
			_logger.LogDebug("OpAmpLoadContext: Assembly '{AssemblyName}' is not targeted for isolation. Falling back to default resolution.",
				assemblyName.Name);

			return null;
		}

		var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);

		if (assemblyPath is not null)
		{
			_logger.LogDebug("OpAmpLoadContext: Resolving assembly '{AssemblyName}' to path '{AssemblyPath}'",
				assemblyName.Name, assemblyPath);

			return LoadFromAssemblyPath(assemblyPath);
		}
		else
		{
			_logger.LogWarning("OpAmpLoadContext: Failed to resolve assembly '{AssemblyName}' using the resolver. Falling back to default resolution.",
				assemblyName.Name);
		}

		return null;
	}

	//private static IsolatedOpAmpLoadContext? _instance;
	//private static readonly object LockObject = new();
	
	//private readonly string _basePath;

	//private IsolatedOpAmpLoadContext(string basePath) : base("ElasticOpenTelemetryIsolatedOpAmp", isCollectible: false) => _basePath = basePath;

	///// <summary>
	///// Gets or creates the singleton isolated load context.
	///// </summary>
	//public static IsolatedOpAmpLoadContext GetOrCreate(string? basePath = null)
	//{
	//	if (_instance != null)
	//		return _instance;

	//	lock (LockObject)
	//	{
	//		if (_instance != null)
	//			return _instance;

	//		basePath ??= AppContext.BaseDirectory;

	//		_instance = new IsolatedOpAmpLoadContext(basePath);
	//		return _instance;
	//	}
	//}

	//protected override Assembly? Load(AssemblyName assemblyName)
	//{
	//	// Only intercept known problematic assemblies that need isolation
	//	if (assemblyName.Name is "Google.Protobuf" or "OpenTelemetry.OpAmp.Client")
	//	{
	//		var assemblyPath = Path.Combine(_basePath, $"{assemblyName.Name}.dll");
			
	//		if (File.Exists(assemblyPath))
	//		{
	//			try
	//			{
	//				return LoadFromAssemblyPath(assemblyPath);
	//			}
	//			catch (Exception ex)
	//			{
	//				System.Diagnostics.Debug.WriteLine(
	//					$"IsolatedOpAmpLoadContext: Failed to load {assemblyName.Name} from {assemblyPath}: {ex.Message}");
	//			}
	//		}
	//	}

	//	// Let default resolution handle other assemblies
	//	return null;
	//}
}

#endif
