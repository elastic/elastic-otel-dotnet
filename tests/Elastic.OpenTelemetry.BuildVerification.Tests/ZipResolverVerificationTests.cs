// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.IO.Compression;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Elastic.OpenTelemetry.BuildVerification.Tests.Helpers;

namespace Elastic.OpenTelemetry.BuildVerification.Tests;

/// <summary>
/// Verifies that the redistributable zip layout supports AssemblyDependencyResolver-based
/// resolution for the isolated OpAmp assemblies.
/// </summary>
public class ZipResolverVerificationTests : IDisposable
{
	private readonly string _extractDir;
	private readonly string? _netDir;
	private readonly bool _zipFound;

	public ZipResolverVerificationTests()
	{
		_extractDir = Path.Combine(Path.GetTempPath(), $"edot-zip-resolver-{Guid.NewGuid():N}");
		Directory.CreateDirectory(_extractDir);

		var distroDir = Path.Combine(DotNetHelper.SolutionRoot, ".artifacts", "elastic-distribution");
		if (!Directory.Exists(distroDir))
		{
			_netDir = null;
			return;
		}

		// Find any redistributable zip (prefer Linux for cross-platform CI)
		var zip = Directory.GetFiles(distroDir, "*.zip")
			.FirstOrDefault(f => !f.EndsWith("-windows.zip"))
			?? Directory.GetFiles(distroDir, "*.zip").FirstOrDefault();

		if (zip is null)
		{
			_netDir = null;
			return;
		}

		_zipFound = true;
		ZipFile.ExtractToDirectory(zip, _extractDir);

		var netDir = Path.Combine(_extractDir, "net");
		// Distinguish "no artifacts" (skip) from "broken zip layout" (fail).
		// If the zip was found but has no net/ subfolder, _netDir stays non-null
		// so SkipIfNoArtifacts passes, and tests fail on missing files — surfacing
		// the layout regression instead of silently skipping.
		_netDir = Directory.Exists(netDir) ? netDir : null;
	}

	public void Dispose()
	{
		// Best-effort cleanup. ALC-loaded assemblies are memory-mapped by the CLR,
		// so file locks may persist even after unloading collectible ALCs. The OS
		// temp directory handles eventual cleanup.
		try
		{
			if (Directory.Exists(_extractDir))
				Directory.Delete(_extractDir, true);
		}
		catch (Exception) when (OperatingSystem.IsWindows())
		{
			// Expected on Windows when assemblies were loaded into an ALC from
			// this directory — both UnauthorizedAccessException and IOException
			// can occur for locked files.
		}
	}

	private void SkipIfNoArtifacts()
	{
		// xUnit v2 does not have Assert.Skip. The $XunitDynamicSkip$ exception message
		// prefix is the v2-compatible mechanism for dynamic test skipping, supported by
		// xunit.runner.visualstudio since v2.4.0.
		if (!_zipFound)
			throw new Exception("$XunitDynamicSkip$Redistributable artifacts not available. Run './build.sh redistribute' first.");

		// If the zip was found but net/ is missing, that's a layout regression — fail, don't skip.
		Assert.NotNull(_netDir);
		Assert.True(Directory.Exists(_netDir),
			$"Redistributable zip was found but the extracted net/ folder is missing at: {Path.Combine(_extractDir, "net")}");
	}

	// ── deps.json presence ───────────────────────────────────────────────

	[Fact]
	public void OpAmpDepsJson_ExistsInNetFolder()
	{
		SkipIfNoArtifacts();

		var depsJson = Path.Combine(_netDir!, "Elastic.OpenTelemetry.OpAmp.deps.json");
		Assert.True(File.Exists(depsJson),
			$"Expected Elastic.OpenTelemetry.OpAmp.deps.json in extracted net/ folder at: {depsJson}");
	}

	// ── Resolver tests ───────────────────────────────────────────────────

	[Fact]
	public void Resolver_ResolvesGoogleProtobuf()
	{
		SkipIfNoArtifacts();

		var resolver = CreateResolver();
		var path = resolver.ResolveAssemblyToPath(new AssemblyName("Google.Protobuf"));

		AssertResolvedWithinNetDir(path, "Google.Protobuf");
	}

	[Fact]
	public void Resolver_ResolvesOpAmpClient()
	{
		SkipIfNoArtifacts();

		var resolver = CreateResolver();
		var path = resolver.ResolveAssemblyToPath(new AssemblyName("OpenTelemetry.OpAmp.Client"));

		AssertResolvedWithinNetDir(path, "OpenTelemetry.OpAmp.Client");
	}

	[Fact]
	public void Resolver_ResolvesOpAmpAssembly()
	{
		SkipIfNoArtifacts();

		// Self-resolution of the root assembly is a runtime implementation detail
		// (the root library is listed in its own deps.json targets section), not a
		// documented API guarantee. If this test starts failing on a future .NET version
		// it is an early-warning canary — the production OpAmpIsolatedLoadContext calls
		// LoadFromAssemblyPath directly for the root assembly and would be unaffected.
		var resolver = CreateResolver();
		var path = resolver.ResolveAssemblyToPath(new AssemblyName("Elastic.OpenTelemetry.OpAmp"));

		AssertResolvedWithinNetDir(path, "Elastic.OpenTelemetry.OpAmp");
	}

	[Fact]
	public void Resolver_ResolvesAbstractions()
	{
		SkipIfNoArtifacts();

		// Abstractions is shipped in the zip and listed in the deps.json as a dependency.
		// The production ALC intentionally does NOT load it into the isolated context
		// (it is excluded from the AssembliesToLoad whitelist in OpAmpIsolatedLoadContext)
		// because IOpAmpClient and IOpAmpClientFactory must be type-identical across the
		// ALC boundary. This test validates the resolver sees it; the whitelist test
		// below validates the ALC ignores it.
		var resolver = CreateResolver();
		var path = resolver.ResolveAssemblyToPath(new AssemblyName("Elastic.OpenTelemetry.OpAmp.Abstractions"));

		AssertResolvedWithinNetDir(path, "Elastic.OpenTelemetry.OpAmp.Abstractions");
	}

	[Fact]
	public void SharedDependency_InDepsJson_ButNotInZip()
	{
		SkipIfNoArtifacts();

		var depsJsonPath = Path.Combine(_netDir!, "Elastic.OpenTelemetry.OpAmp.deps.json");
		Assert.True(File.Exists(depsJsonPath), $"deps.json not found: {depsJsonPath}");

		// Verify Microsoft.Extensions.Logging.Abstractions IS listed in the deps.json
		// (proves deps.json completeness for shared dependencies).
		using var depsJson = JsonDocument.Parse(File.ReadAllText(depsJsonPath));
		var targets = depsJson.RootElement.GetProperty("targets");
		var targetFramework = targets.EnumerateObject().First().Value;

		var hasLoggingAbstractions = targetFramework.EnumerateObject()
			.Any(entry => entry.Name.StartsWith("Microsoft.Extensions.Logging.Abstractions/",
				StringComparison.OrdinalIgnoreCase));

		Assert.True(hasLoggingAbstractions,
			"Expected Microsoft.Extensions.Logging.Abstractions to be listed in deps.json targets");

		// Verify the assembly is NOT physically in the extracted net/ folder.
		// It is intentionally excluded from the zip because at runtime it is
		// provided by the host/default ALC.
		var assemblyPath = Path.Combine(_netDir!, "Microsoft.Extensions.Logging.Abstractions.dll");
		Assert.False(File.Exists(assemblyPath),
			"Microsoft.Extensions.Logging.Abstractions.dll should NOT be in the extracted net/ folder");
	}

	// ── ALC load tests ──────────────────────────────────────────────────

	[Fact]
	public void CustomAlc_LoadsIsolatedAssembliesFromExtractedNetFolder()
	{
		SkipIfNoArtifacts();

		var rootAssemblyPath = Path.Combine(_netDir!, "Elastic.OpenTelemetry.OpAmp.dll");
		var loadContext = new TestPluginLoadContext(rootAssemblyPath);

		try
		{
			foreach (var assemblyName in new[]
			{
				"Elastic.OpenTelemetry.OpAmp",
				"OpenTelemetry.OpAmp.Client",
				"Google.Protobuf"
			})
			{
				var assembly = loadContext.LoadFromAssemblyName(new AssemblyName(assemblyName));

				Assert.NotNull(assembly);
				Assert.True(
					assembly.Location.StartsWith(_netDir!, StringComparison.OrdinalIgnoreCase),
					$"Expected assembly '{assemblyName}' to load from '{_netDir}' but was at '{assembly.Location}'");
				Assert.Same(loadContext, AssemblyLoadContext.GetLoadContext(assembly)!);
			}
		}
		finally
		{
			// Unload the collectible ALC to release file locks on the extracted assemblies.
			loadContext.Unload();
		}
	}

	[Fact]
	public void WhitelistedAlc_DoesNotLoadNonWhitelistedAssemblies()
	{
		SkipIfNoArtifacts();

		// WARNING: Default ALC pollution scope.
		// LoadFromAssemblyPath into AssemblyLoadContext.Default is irreversible for the
		// process lifetime. If any other test in the same test run resolves
		// Elastic.OpenTelemetry.OpAmp.Abstractions by name, it will get THIS copy
		// (from the extracted zip) rather than a build output copy. Currently safe
		// because this test project has no reference to OpAmp.Abstractions. If that
		// changes, consider isolating this test in a child process.
		//
		// This mirrors production, where Abstractions is available in the default ALC
		// because AutoInstrumentation.dll links it in. Pre-loading is necessary so
		// the fallback succeeds when the whitelist ALC correctly returns null.
		var abstractionsPath = Path.Combine(_netDir!, "Elastic.OpenTelemetry.OpAmp.Abstractions.dll");
		Assert.True(File.Exists(abstractionsPath), $"Abstractions assembly not found: {abstractionsPath}");
		AssemblyLoadContext.Default.LoadFromAssemblyPath(abstractionsPath);

		var rootAssemblyPath = Path.Combine(_netDir!, "Elastic.OpenTelemetry.OpAmp.dll");
		var loadContext = new WhitelistedPluginLoadContext(rootAssemblyPath);

		try
		{
			// Abstractions is resolvable but not in the whitelist — Load returns null,
			// delegating to the default ALC where we pre-loaded it.
			var assembly = loadContext.LoadFromAssemblyName(new AssemblyName("Elastic.OpenTelemetry.OpAmp.Abstractions"));

			Assert.NotNull(assembly);
			Assert.Same(AssemblyLoadContext.Default, AssemblyLoadContext.GetLoadContext(assembly)!);
		}
		finally
		{
			loadContext.Unload();
		}
	}

	// ── Helpers ──────────────────────────────────────────────────────────

	private AssemblyDependencyResolver CreateResolver()
	{
		var rootAssemblyPath = Path.Combine(_netDir!, "Elastic.OpenTelemetry.OpAmp.dll");
		Assert.True(File.Exists(rootAssemblyPath),
			$"Root assembly not found: {rootAssemblyPath}");
		return new AssemblyDependencyResolver(rootAssemblyPath);
	}

	private void AssertResolvedWithinNetDir(string? path, string assemblyName)
	{
		Assert.NotNull(path);
		Assert.True(File.Exists(path), $"Resolved path does not exist: {path}");
		Assert.True(
			path.StartsWith(_netDir!, StringComparison.OrdinalIgnoreCase),
			$"Resolved path for '{assemblyName}' is not within extracted net/ folder. " +
			$"Expected prefix: '{_netDir}', actual: '{path}'");
	}

	// ── Test ALC variants ────────────────────────────────────────────────

	/// <summary>
	/// Loads everything the resolver can find (validates resolver + flat layout).
	/// Uses isCollectible: true for test cleanup (production OpAmpIsolatedLoadContext uses false).
	/// The collectible difference does not affect assembly resolution or loading behavior.
	/// </summary>
	private sealed class TestPluginLoadContext : AssemblyLoadContext
	{
		private readonly AssemblyDependencyResolver _resolver;

		public TestPluginLoadContext(string rootAssemblyPath)
			: base($"TG014:{Path.GetFileName(rootAssemblyPath)}", isCollectible: true)
			=> _resolver = new AssemblyDependencyResolver(rootAssemblyPath);

		protected override Assembly? Load(AssemblyName assemblyName)
		{
			var path = _resolver.ResolveAssemblyToPath(assemblyName);
			return path is null ? null : LoadFromAssemblyPath(path);
		}
	}

	/// <summary>
	/// Mirrors the production <c>OpAmpIsolatedLoadContext</c> whitelist filter
	/// (validates isolation contract).
	/// </summary>
	private sealed class WhitelistedPluginLoadContext : AssemblyLoadContext
	{
		// SYNC: Must match OpAmpIsolatedLoadContext.AssembliesToLoad (OpAmpIsolatedLoadContext.cs:35-40).
		// Production uses OpAmpClientContract constants (OpAmpClientContract.cs:19-24):
		//   - OpAmpClientContract.ProtobufAssemblyName ("Google.Protobuf")
		//   - OpAmpClientContract.OpAmpClientAssemblyName ("OpenTelemetry.OpAmp.Client")
		//   - OpAmpClientContract.AssemblyName ("Elastic.OpenTelemetry.OpAmp")
		// If the production whitelist changes, this test must be updated to match.
		private static readonly string[] AssembliesToLoad =
		[
			"Google.Protobuf",
			"OpenTelemetry.OpAmp.Client",
			"Elastic.OpenTelemetry.OpAmp"
		];

		private readonly AssemblyDependencyResolver _resolver;

		public WhitelistedPluginLoadContext(string rootAssemblyPath)
			: base($"TG014-whitelist:{Path.GetFileName(rootAssemblyPath)}", isCollectible: true)
			=> _resolver = new AssemblyDependencyResolver(rootAssemblyPath);

		protected override Assembly? Load(AssemblyName assemblyName)
		{
			var name = assemblyName.Name;
			if (string.IsNullOrWhiteSpace(name) || !AssembliesToLoad.Contains(name))
				return null; // Delegate to default ALC — mirrors production behavior

			var path = _resolver.ResolveAssemblyToPath(assemblyName);
			return path is null ? null : LoadFromAssemblyPath(path);
		}
	}
}
