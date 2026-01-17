// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

#if NET8_0_OR_GREATER || NETSTANDARD2_1

#pragma warning disable IL2026 // Using reflection with LoadFromAssemblyPath is intentional for OpAmp isolation

using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

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
/// Note: This class is only compiled when NET8_0_OR_GREATER or NETSTANDARD2_1 is defined,
/// as those are the only frameworks that support AssemblyLoadContext.
/// </summary>
internal sealed class IsolatedOpAmpLoadContext : AssemblyLoadContext
{
	private static IsolatedOpAmpLoadContext? _instance;
	private static readonly object LockObject = new();
	
	private readonly string _basePath;

	private IsolatedOpAmpLoadContext(string basePath) : base("ElasticOpenTelemetryIsolatedOpAmp", isCollectible: false) => _basePath = basePath;

	/// <summary>
	/// Gets or creates the singleton isolated load context.
	/// </summary>
	public static IsolatedOpAmpLoadContext GetOrCreate(string? basePath = null)
	{
		if (_instance != null)
			return _instance;

		lock (LockObject)
		{
			if (_instance != null)
				return _instance;

			basePath ??= AppContext.BaseDirectory;

			_instance = new IsolatedOpAmpLoadContext(basePath);
			return _instance;
		}
	}

	protected override Assembly? Load(AssemblyName assemblyName)
	{
		// Only intercept known problematic assemblies that need isolation
		if (assemblyName.Name is "Google.Protobuf" or "OpenTelemetry.OpAmp.Client")
		{
			var assemblyPath = Path.Combine(_basePath, $"{assemblyName.Name}.dll");
			
			if (File.Exists(assemblyPath))
			{
				try
				{
					return LoadFromAssemblyPath(assemblyPath);
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine(
						$"IsolatedOpAmpLoadContext: Failed to load {assemblyName.Name} from {assemblyPath}: {ex.Message}");
				}
			}
		}

		// Let default resolution handle other assemblies
		return null;
	}
}

#pragma warning restore IL2026

#endif
