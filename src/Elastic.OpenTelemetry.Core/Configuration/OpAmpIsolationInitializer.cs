// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Reflection;

namespace Elastic.OpenTelemetry.Core.Configuration;

/// <summary>
/// Initializes isolated loading of OpAmp and Protobuf dependencies to prevent version conflicts.
/// 
/// When USE_ISOLATED_OPAMP_CLIENT is defined (for AutoInstrumentation on modern .NET),
/// this initializer ensures OpAmp and Protobuf are loaded in an isolated AssemblyLoadContext.
/// 
/// The actual isolation is only active on frameworks that support AssemblyLoadContext (net8.0+, netstandard2.1).
/// </summary>
internal static class OpAmpIsolationInitializer
{
	private static bool _initialized;
	private static readonly object LockObject = new();

	/// <summary>
	/// Initializes the isolated AssemblyLoadContext for OpAmp dependencies if configured and supported.
	/// 
	/// Safe to call multiple times - only initializes once.
	/// This method is safe to call from any framework - isolation is only attempted if supported.
	/// </summary>
	public static void Initialize()
	{
		if (_initialized)
			return;

		lock (LockObject)
		{
			if (_initialized)
				return;

#if USE_ISOLATED_OPAMP_CLIENT && (NET8_0_OR_GREATER || NETSTANDARD2_1)
			TryInitializeIsolation();
#endif

			_initialized = true;
		}
	}

#if USE_ISOLATED_OPAMP_CLIENT && (NET8_0_OR_GREATER || NETSTANDARD2_1)

	private static void TryInitializeIsolation()
	{
		try
		{
			var context = IsolatedOpAmpLoadContext.GetOrCreate();
			
			// Pre-load the assemblies into the isolated context.
			// This ensures they're always resolved from there, preventing version conflicts
			// with the application's own versions in the default ALC.
			try
			{
				context.LoadFromAssemblyName(new AssemblyName("Google.Protobuf"));
			}
			catch (Exception ex)
			{
				// May already be loaded or not present
				System.Diagnostics.Debug.WriteLine($"OpAmpIsolationInitializer: Failed to pre-load Google.Protobuf: {ex.Message}");
			}

			try
			{
				context.LoadFromAssemblyName(new AssemblyName("OpenTelemetry.OpAmp.Client"));
			}
			catch (Exception ex)
			{
				// May not be present
				System.Diagnostics.Debug.WriteLine($"OpAmpIsolationInitializer: Failed to pre-load OpenTelemetry.OpAmp.Client: {ex.Message}");
			}

			System.Diagnostics.Debug.WriteLine("OpAmpIsolationInitializer: OpAmp dependencies loaded in isolated context");
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"OpAmpIsolationInitializer: Failed to initialize OpAmp isolation: {ex.Message}");
			// Continue without isolation if it fails - OpAmp will work, just without version conflict protection
		}
	}

#endif
}
