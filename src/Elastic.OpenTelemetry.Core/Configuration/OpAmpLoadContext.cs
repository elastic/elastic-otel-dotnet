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

	public OpAmpLoadContext(ILogger logger) : base("ElasticOpenTelemetryIsolatedOpAmp", isCollectible: false)
	{
		_logger = logger;

		var otelInstallationPath = Environment.GetEnvironmentVariable("OTEL_DOTNET_AUTO_INSTALL_DIR");

		if (string.IsNullOrEmpty(otelInstallationPath))
		{
			_logger.LogWarning("OpAmpLoadContext: OTEL_DOTNET_AUTO_INSTALL_DIR environment variable is not set. " +
				"Falling back to default assembly resolution which may lead to version conflicts.");

			return;
		}

		_otelInstallationPath = Path.Join(otelInstallationPath, "net", GetType().Assembly.GetName().Name);

		// TODO - Check path exists

		_logger.LogDebug("OpAmpLoadContext: Initializing isolated load context for OpenTelemetry OpAmp dependencies for '{OtelInstallationPath}'",
			otelInstallationPath ?? "<null>");

		_resolver = new AssemblyDependencyResolver(otelInstallationPath!);
		
		// Hook into AssemblyResolve to handle version mismatches
		Resolving += OnAssemblyResolve;
	}

	public string? OtelInstallationPath => _otelInstallationPath;

	private System.Reflection.Assembly? OnAssemblyResolve(AssemblyLoadContext context, AssemblyName assemblyName)
	{
		if (assemblyName.Name is not "Google.Protobuf" and not "OpenTelemetry.OpAmp.Client")
		{
			return null;
		}

		_logger.LogDebug("OpAmpLoadContext.OnAssemblyResolve() called for: '{AssemblyName}' version {Version}",
			assemblyName.Name, assemblyName.Version);

		// Try to load from the resolver first
		if (_resolver is not null)
		{
			try
			{
				var resolvedPath = _resolver.ResolveAssemblyToPath(assemblyName);
				if (resolvedPath is not null && File.Exists(resolvedPath))
				{
					_logger.LogDebug("OpAmpLoadContext.OnAssemblyResolve: Resolver found '{AssemblyName}' at {ResolvedPath}",
						assemblyName.Name, resolvedPath);
#pragma warning disable IL2026
					return LoadFromAssemblyPath(resolvedPath);
#pragma warning restore IL2026
				}
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "OpAmpLoadContext.OnAssemblyResolve: Resolver failed for '{AssemblyName}'", assemblyName.Name);
			}
		}

		// Fallback: try direct path resolution
		if (!string.IsNullOrEmpty(_otelInstallationPath))
		{
			var assemblyPath = Path.Combine(_otelInstallationPath, $"{assemblyName.Name}.dll");
			if (File.Exists(assemblyPath))
			{
				try
				{
					_logger.LogDebug("OpAmpLoadContext.OnAssemblyResolve: Loading '{AssemblyName}' from direct path: {AssemblyPath}",
						assemblyName.Name, assemblyPath);
#pragma warning disable IL2026
					return LoadFromAssemblyPath(assemblyPath);
#pragma warning restore IL2026
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "OpAmpLoadContext.OnAssemblyResolve: Failed to load '{AssemblyName}' from {AssemblyPath}",
						assemblyName.Name, assemblyPath);
				}
			}
		}

		_logger.LogDebug("OpAmpLoadContext.OnAssemblyResolve: Could not resolve '{AssemblyName}' version {Version}",
			assemblyName.Name, assemblyName.Version);

		return null;
	}

	[UnconditionalSuppressMessage("AssemblyLoadTrimming", "IL2026: RequiresUnreferencedCode", Justification = "The calls to this ALC will be guarded by a runtime check")]
	protected override Assembly? Load(AssemblyName assemblyName)
	{
		_logger.LogDebug("OpAmpLoadContext.Load() called for assembly: '{AssemblyName}' version {Version}", 
			assemblyName.Name, assemblyName.Version);

		// Only intercept known problematic assemblies that need isolation
		if (assemblyName.Name is not "Google.Protobuf" and not "OpenTelemetry.OpAmp.Client")
		{
			_logger.LogDebug("OpAmpLoadContext: Assembly '{AssemblyName}' is not targeted for isolation. Falling back to default resolution.",
				assemblyName.Name);

			return null;
		}

		if (_resolver is null)
		{
			_logger.LogDebug("OpAmpLoadContext: Resolver is not initialized for '{AssemblyName}'. Attempting direct path resolution.",
				assemblyName.Name);

			// Try direct path resolution if resolver is not initialized
			if (!string.IsNullOrEmpty(_otelInstallationPath))
			{
				var assemblyPath = Path.Combine(_otelInstallationPath, $"{assemblyName.Name}.dll");
				if (File.Exists(assemblyPath))
				{
					try
					{
						_logger.LogDebug("OpAmpLoadContext: Loading '{AssemblyName}' from path: {AssemblyPath}", 
							assemblyName.Name, assemblyPath);
#pragma warning disable IL2026
						return LoadFromAssemblyPath(assemblyPath);
#pragma warning restore IL2026
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "OpAmpLoadContext: Failed to load '{AssemblyName}' from {AssemblyPath}", 
							assemblyName.Name, assemblyPath);
					}
				}
			}

			return null;
		}

		var resolvedPath = _resolver.ResolveAssemblyToPath(assemblyName);

		if (resolvedPath is not null)
		{
			_logger.LogDebug("OpAmpLoadContext: Resolver resolved '{AssemblyName}' to path: {ResolvedPath}",
				assemblyName.Name, resolvedPath);

			try
			{
				return LoadFromAssemblyPath(resolvedPath);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "OpAmpLoadContext: Failed to load '{AssemblyName}' from resolved path: {ResolvedPath}", 
					assemblyName.Name, resolvedPath);
				throw;
			}
		}
		else
		{
			_logger.LogWarning("OpAmpLoadContext: Resolver could not resolve '{AssemblyName}' version {Version}. Falling back to default resolution.",
				assemblyName.Name, assemblyName.Version);
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
