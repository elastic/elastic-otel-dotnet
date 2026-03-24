// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.BuildVerification.Tests.Helpers;

namespace Elastic.OpenTelemetry.BuildVerification.Tests;

/// <summary>
/// Builds assemblies with specific properties and inspects them via PE metadata
/// to verify the correct types are present/absent based on conditional compilation.
/// </summary>
[Collection("BuildArtifacts")]
public class AssemblyTypeInspectionTests : IAsyncLifetime
{
	private const string AutoInstProject =
		"src/Elastic.OpenTelemetry.AutoInstrumentation/Elastic.OpenTelemetry.AutoInstrumentation.csproj";

	private const string ElasticOTelProject =
		"src/Elastic.OpenTelemetry/Elastic.OpenTelemetry.csproj";

	public async Task InitializeAsync()
	{
		// Build AutoInstrumentation for zip + net8.0 (ALC isolation)
		var result = await DotNetHelper.BuildAsync(AutoInstProject, new()
		{
			["TargetFramework"] = "net8.0",
			["BuildingForZipDistribution"] = "true"
		});
		Assert.True(result.ExitCode == 0,
			$"Build failed (AutoInstrumentation zip net8.0):\n{result.Error}");

		// Build AutoInstrumentation for zip + net462 (source compiled in)
		result = await DotNetHelper.BuildAsync(AutoInstProject, new()
		{
			["TargetFramework"] = "net462",
			["BuildingForZipDistribution"] = "true"
		});
		Assert.True(result.ExitCode == 0,
			$"Build failed (AutoInstrumentation zip net462):\n{result.Error}");

		// Build Elastic.OpenTelemetry for net8.0 (NuGet scenario)
		result = await DotNetHelper.BuildAsync(ElasticOTelProject, new()
		{
			["TargetFramework"] = "net8.0"
		});
		Assert.True(result.ExitCode == 0,
			$"Build failed (Elastic.OpenTelemetry net8.0):\n{result.Error}");

		// Build Elastic.OpenTelemetry for net9.0 (NuGet scenario)
		result = await DotNetHelper.BuildAsync(ElasticOTelProject, new()
		{
			["TargetFramework"] = "net9.0"
		});
		Assert.True(result.ExitCode == 0,
			$"Build failed (Elastic.OpenTelemetry net9.0):\n{result.Error}");
	}

	public Task DisposeAsync() => Task.CompletedTask;

	// ── AutoInstrumentation — Zip + net8.0 ────────────────────────────────

	[Fact]
	public void AutoInstrumentation_ZipNet80_ContainsIsolatedLoadContext()
	{
		var path = GetAssemblyPath("Elastic.OpenTelemetry.AutoInstrumentation", "release_net8.0");
		Assert.True(File.Exists(path), $"Assembly not found: {path}");
		Assert.True(AssemblyHelper.ContainsType(path, "OpAmpIsolatedLoadContext"),
			"AutoInstrumentation net8.0 zip build should contain OpAmpIsolatedLoadContext for ALC isolation");
	}

	[Fact]
	public void AutoInstrumentation_ZipNet80_DoesNotContainElasticOpAmpClient()
	{
		var path = GetAssemblyPath("Elastic.OpenTelemetry.AutoInstrumentation", "release_net8.0");
		Assert.True(File.Exists(path), $"Assembly not found: {path}");
		Assert.False(AssemblyHelper.ContainsType(path, "ElasticOpAmpClient"),
			"AutoInstrumentation net8.0 zip build should NOT embed ElasticOpAmpClient (it lives in a separate assembly for ALC)");
	}

	// ── AutoInstrumentation — Zip + net462 ────────────────────────────────

	[Fact]
	public void AutoInstrumentation_ZipNet462_ContainsElasticOpAmpClient()
	{
		var path = GetAssemblyPath("Elastic.OpenTelemetry.AutoInstrumentation", "release_net462");
		Assert.True(File.Exists(path), $"Assembly not found: {path}");
		Assert.True(AssemblyHelper.ContainsType(path, "ElasticOpAmpClient"),
			"AutoInstrumentation net462 zip build should contain ElasticOpAmpClient (source compiled in)");
	}

	[Fact]
	public void AutoInstrumentation_ZipNet462_DoesNotContainIsolatedLoadContext()
	{
		var path = GetAssemblyPath("Elastic.OpenTelemetry.AutoInstrumentation", "release_net462");
		Assert.True(File.Exists(path), $"Assembly not found: {path}");
		Assert.False(AssemblyHelper.ContainsType(path, "OpAmpIsolatedLoadContext"),
			"AutoInstrumentation net462 zip build should NOT contain OpAmpIsolatedLoadContext (no ALC on .NET Framework)");
	}

	// ── Elastic.OpenTelemetry — NuGet net8.0 ──────────────────────────────

	[Fact]
	public void ElasticOpenTelemetry_Net80_ContainsElasticOpAmpClient()
	{
		var path = GetAssemblyPath("Elastic.OpenTelemetry", "release_net8.0");
		Assert.True(File.Exists(path), $"Assembly not found: {path}");
		Assert.True(AssemblyHelper.ContainsType(path, "ElasticOpAmpClient"),
			"Elastic.OpenTelemetry net8.0 should contain ElasticOpAmpClient (source compiled in for NuGet)");
	}

	[Fact]
	public void ElasticOpenTelemetry_Net80_DoesNotContainIsolatedLoadContext()
	{
		var path = GetAssemblyPath("Elastic.OpenTelemetry", "release_net8.0");
		Assert.True(File.Exists(path), $"Assembly not found: {path}");
		Assert.False(AssemblyHelper.ContainsType(path, "OpAmpIsolatedLoadContext"),
			"Elastic.OpenTelemetry NuGet package should NOT contain OpAmpIsolatedLoadContext (no ALC isolation for NuGet consumers)");
	}

	// ── Elastic.OpenTelemetry — NuGet net9.0 ──────────────────────────────

	[Fact]
	public void ElasticOpenTelemetry_Net90_ContainsElasticOpAmpClient()
	{
		var path = GetAssemblyPath("Elastic.OpenTelemetry", "release_net9.0");
		Assert.True(File.Exists(path), $"Assembly not found: {path}");
		Assert.True(AssemblyHelper.ContainsType(path, "ElasticOpAmpClient"),
			"Elastic.OpenTelemetry net9.0 should contain ElasticOpAmpClient (source compiled in for NuGet)");
	}

	[Fact]
	public void ElasticOpenTelemetry_Net90_DoesNotContainIsolatedLoadContext()
	{
		var path = GetAssemblyPath("Elastic.OpenTelemetry", "release_net9.0");
		Assert.True(File.Exists(path), $"Assembly not found: {path}");
		Assert.False(AssemblyHelper.ContainsType(path, "OpAmpIsolatedLoadContext"),
			"Elastic.OpenTelemetry NuGet package should NOT contain OpAmpIsolatedLoadContext (no ALC isolation for NuGet consumers)");
	}

	private static string GetAssemblyPath(string projectName, string configTfm) =>
		Path.Combine(
			DotNetHelper.SolutionRoot,
			".artifacts", "bin", projectName, configTfm,
			$"{projectName}.dll");
}
