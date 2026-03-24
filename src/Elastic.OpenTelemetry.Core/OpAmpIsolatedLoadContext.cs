// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

#if NET && USE_ISOLATED_OPAMP_CLIENT

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;
using Elastic.OpenTelemetry.Core.Configuration;
using Elastic.OpenTelemetry.OpAmp.Abstractions;
using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.OpAmp;

/// <summary>
/// An <c>AssemblyLoadContext</c> that creates OpAmp adapters in an isolated context.
/// This allows us to load the OpAmp assemblies and create instances that directly use
/// OpenTelemetry.OpAmp.Client types without proxy overhead.
/// </summary>
internal sealed class OpAmpIsolatedLoadContext : AssemblyLoadContext
{
	/// <summary>
	/// Assemblies to load in the isolated ALC.
	/// </summary>
	/// <remarks>
	/// IMPORTANT: Only add assemblies that need version isolation here.
	/// Shared framework assemblies (e.g., Microsoft.Extensions.Logging.Abstractions)
	/// must NOT be added — they must resolve from the default ALC so that types
	/// like <see cref="ILogger"/> and <c>IOpAmpClientFactory</c> remain
	/// reference-equal across the ALC boundary.
	/// If a shared type is loaded in both ALCs, interface casts will fail
	/// with InvalidCastException at runtime.
	/// </remarks>
	private static readonly string[] AssembliesToLoad =
	[
		OpAmpClientContract.ProtobufAssemblyName,
		OpAmpClientContract.OpAmpClientAssemblyName,
		OpAmpClientContract.AssemblyName
	];

	private static readonly string? DefaultOtelInstallationPath = Environment.GetEnvironmentVariable("OTEL_DOTNET_AUTO_INSTALL_DIR");

	private readonly ILogger _logger;
	private readonly AssemblyDependencyResolver? _resolver;

	internal OpAmpIsolatedLoadContext(ILogger logger)
		: base("ElasticOpAmpAdapter", isCollectible: false)
	{
		_logger = logger;

#if DEBUG
		var opAmpPath = Path.Join(AppContext.BaseDirectory, "Elastic.OpenTelemetry.OpAmp.dll");
		if (!File.Exists(opAmpPath))
		{
			throw new FileNotFoundException(
				$"{nameof(OpAmpIsolatedLoadContext)}: OpAmp DLL not found at: {opAmpPath} " +
				$"(BaseDirectory: {AppContext.BaseDirectory}). " +
				"Ensure the project is built with -f net8.0 -p:BuildingForZipDistribution=true.",
				opAmpPath);
		}

		try
		{
			_resolver = new AssemblyDependencyResolver(opAmpPath);
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException(
				$"{nameof(OpAmpIsolatedLoadContext)}: Failed to create AssemblyDependencyResolver for: {opAmpPath}", ex);
		}
#else
		if (string.IsNullOrEmpty(DefaultOtelInstallationPath))
		{
			_logger.LogWarning("{ClassName}: OTEL_DOTNET_AUTO_INSTALL_DIR not set. Falling back to default assembly resolution.", nameof(OpAmpIsolatedLoadContext));
			return;
		}

		var netPath = Path.Join(DefaultOtelInstallationPath, "net");
		if (!Directory.Exists(netPath))
		{
			_logger.LogWarning("{ClassName}: Expected net subdirectory not found at: {NetPath}", nameof(OpAmpIsolatedLoadContext), netPath);
			return;
		}

		var opAmpPath = Path.Join(netPath, "Elastic.OpenTelemetry.OpAmp.dll");
		if (!File.Exists(opAmpPath))
		{
			_logger.LogWarning("{ClassName}: OpAMP DLL not found at: {OpAmpPath}", nameof(OpAmpIsolatedLoadContext), opAmpPath);
			return;
		}

		_logger.LogDebug("{ClassName}: Initializing isolated load context for OpAmp adapter at: {NetPath}", nameof(OpAmpIsolatedLoadContext), netPath);

		try
		{
			_resolver = new AssemblyDependencyResolver(opAmpPath);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "{ClassName}: Failed to create AssemblyDependencyResolver for: {OpAmpPath}", nameof(OpAmpIsolatedLoadContext), opAmpPath);
		}
#endif
	}

	[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "Usage of OpAmpIsolatedLoadContext is " +
		"guarded by a RuntimeFeature.IsDynamicCodeSupported` check and therefore this method is never called in AoT scenarios.")]
	protected override Assembly? Load(AssemblyName assemblyName)
	{
		if (_resolver is null)
			return null;

		var assemblySimpleName = assemblyName.Name;
		if (string.IsNullOrWhiteSpace(assemblySimpleName))
			return null;

		// Check for shared assemblies — return null so they're loaded by the default AssemblyLoadContext.
		if (!AssembliesToLoad.Contains(assemblySimpleName))
			return null;

		try
		{
			var path = _resolver.ResolveAssemblyToPath(assemblyName);
			if (path is not null && File.Exists(path))
			{
				_logger.LogAssemblyResolved(nameof(OpAmpIsolatedLoadContext), nameof(Load), assemblySimpleName, path);
				return LoadFromAssemblyPath(path);
			}

			if (path is null)
			{
				_logger.LogAssemblyResolverReturnedNull(nameof(OpAmpIsolatedLoadContext),
					nameof(Load), assemblySimpleName);
			}
			else
			{
				_logger.LogAssemblyResolvedPathNotFound(nameof(OpAmpIsolatedLoadContext),
					nameof(Load), assemblySimpleName, path);
			}
		}
		catch (Exception ex)
		{
			_logger.LogAssemblyResolutionFailed(ex, nameof(OpAmpIsolatedLoadContext), nameof(Load), assemblySimpleName);
		}

		return null;
	}

	[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode", Justification = "Usage of OpAmpIsolatedLoadContext is " +
		"guarded by a RuntimeFeature.IsDynamicCodeSupported` check and therefore this method is never called in AoT scenarios.")]
	[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2072:UnrecognizedReflectionPattern", Justification = "Usage of OpAmpIsolatedLoadContext is " +
		"guarded by a RuntimeFeature.IsDynamicCodeSupported` check and therefore this method is never called in AoT scenarios.")]
	internal IOpAmpClient? CreateOpAmpClientInstance(
		ILogger logger, string endPoint, string headers,
		string serviceName, string? serviceVersion, string userAgent)
	{
		var loadedAssembly = LoadFromAssemblyName(new AssemblyName(OpAmpClientContract.AssemblyName));
		var factoryType = loadedAssembly?.GetType(OpAmpClientContract.FactoryTypeName);

		if (factoryType is null)
		{
			_logger.LogFactoryTypeNotFound(nameof(OpAmpIsolatedLoadContext), nameof(CreateOpAmpClientInstance),
				OpAmpClientContract.FactoryTypeName);
			return null;
		}

		try
		{
			var factory = (IOpAmpClientFactory?)Activator.CreateInstance(factoryType);

			if (factory is null)
			{
				_logger.LogFactoryActivationFailed(nameof(OpAmpIsolatedLoadContext), nameof(CreateOpAmpClientInstance),
					OpAmpClientContract.FactoryTypeName);
				return null;
			}

			return factory.Create(logger, endPoint, headers, serviceName, serviceVersion, userAgent);
		}
		catch (Exception ex)
		{
			_logger.LogFactoryCreateException(ex, nameof(OpAmpIsolatedLoadContext), nameof(CreateOpAmpClientInstance),
				OpAmpClientContract.FactoryTypeName);
			return null;
		}
	}
}
#endif
