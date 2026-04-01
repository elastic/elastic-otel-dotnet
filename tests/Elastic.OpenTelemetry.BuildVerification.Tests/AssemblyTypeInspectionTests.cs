// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.BuildVerification.Tests.Helpers;
using Xunit.Abstractions;

// All tests in this class rely on BuildArtifactsFixture which runs full
// dotnet build + pack and consistently times out on CI (>20 min budget).

namespace Elastic.OpenTelemetry.BuildVerification.Tests;

/// <summary>
/// Inspects built assemblies via PE metadata to verify the correct types are
/// present/absent based on conditional compilation. Builds are performed once
/// by <see cref="BuildArtifactsFixture"/>.
/// </summary>
[Collection("BuildArtifacts")]
public class AssemblyTypeInspectionTests(BuildArtifactsFixture fixture, ITestOutputHelper output)
{
	private void AssertFixtureReady()
	{
		output.WriteLine($"Fixture state: {(fixture.InitializationError is null ? "ready" : fixture.InitializationError)}");
		Assert.Null(fixture.InitializationError);
	}

	// ── AutoInstrumentation — Zip + net8.0 ────────────────────────────────

	[SkipOnCiFact("BuildArtifactsFixture times out on CI; needs investigation.")]
	public void AutoInstrumentation_ZipNet80_ContainsIsolatedLoadContext()
	{
		AssertFixtureReady();
		var path = GetAssemblyPath("Elastic.OpenTelemetry.AutoInstrumentation", "release_net8.0");
		output.WriteLine($"Inspecting: {path}");
		Assert.True(File.Exists(path), $"Assembly not found: {path}");
		Assert.True(AssemblyHelper.ContainsType(path, "OpAmpIsolatedLoadContext"),
			"AutoInstrumentation net8.0 zip build should contain OpAmpIsolatedLoadContext for ALC isolation");
	}

	[SkipOnCiFact("BuildArtifactsFixture times out on CI; needs investigation.")]
	public void AutoInstrumentation_ZipNet80_DoesNotContainElasticOpAmpClient()
	{
		AssertFixtureReady();
		var path = GetAssemblyPath("Elastic.OpenTelemetry.AutoInstrumentation", "release_net8.0");
		output.WriteLine($"Inspecting: {path}");
		Assert.True(File.Exists(path), $"Assembly not found: {path}");
		Assert.False(AssemblyHelper.ContainsType(path, "ElasticOpAmpClient"),
			"AutoInstrumentation net8.0 zip build should NOT embed ElasticOpAmpClient (it lives in a separate assembly for ALC)");
	}

	// ── AutoInstrumentation — Zip + net462 ────────────────────────────────

	[SkipOnCiFact("BuildArtifactsFixture times out on CI; needs investigation.")]
	public void AutoInstrumentation_ZipNet462_ContainsElasticOpAmpClient()
	{
		AssertFixtureReady();
		var path = GetAssemblyPath("Elastic.OpenTelemetry.AutoInstrumentation", "release_net462");
		output.WriteLine($"Inspecting: {path}");
		Assert.True(File.Exists(path), $"Assembly not found: {path}");
		Assert.True(AssemblyHelper.ContainsType(path, "ElasticOpAmpClient"),
			"AutoInstrumentation net462 zip build should contain ElasticOpAmpClient (source compiled in)");
	}

	[SkipOnCiFact("BuildArtifactsFixture times out on CI; needs investigation.")]
	public void AutoInstrumentation_ZipNet462_DoesNotContainIsolatedLoadContext()
	{
		AssertFixtureReady();
		var path = GetAssemblyPath("Elastic.OpenTelemetry.AutoInstrumentation", "release_net462");
		output.WriteLine($"Inspecting: {path}");
		Assert.True(File.Exists(path), $"Assembly not found: {path}");
		Assert.False(AssemblyHelper.ContainsType(path, "OpAmpIsolatedLoadContext"),
			"AutoInstrumentation net462 zip build should NOT contain OpAmpIsolatedLoadContext (no ALC on .NET Framework)");
	}

	// ── Elastic.OpenTelemetry — NuGet net8.0 ──────────────────────────────

	[SkipOnCiFact("BuildArtifactsFixture times out on CI; needs investigation.")]
	public void ElasticOpenTelemetry_Net80_ContainsElasticOpAmpClient()
	{
		AssertFixtureReady();
		var path = GetAssemblyPath("Elastic.OpenTelemetry", "release_net8.0");
		output.WriteLine($"Inspecting: {path}");
		Assert.True(File.Exists(path), $"Assembly not found: {path}");
		Assert.True(AssemblyHelper.ContainsType(path, "ElasticOpAmpClient"),
			"Elastic.OpenTelemetry net8.0 should contain ElasticOpAmpClient (source compiled in for NuGet)");
	}

	[SkipOnCiFact("BuildArtifactsFixture times out on CI; needs investigation.")]
	public void ElasticOpenTelemetry_Net80_DoesNotContainIsolatedLoadContext()
	{
		AssertFixtureReady();
		var path = GetAssemblyPath("Elastic.OpenTelemetry", "release_net8.0");
		output.WriteLine($"Inspecting: {path}");
		Assert.True(File.Exists(path), $"Assembly not found: {path}");
		Assert.False(AssemblyHelper.ContainsType(path, "OpAmpIsolatedLoadContext"),
			"Elastic.OpenTelemetry NuGet package should NOT contain OpAmpIsolatedLoadContext (no ALC isolation for NuGet consumers)");
	}

	// ── Elastic.OpenTelemetry — NuGet net9.0 ──────────────────────────────

	[SkipOnCiFact("BuildArtifactsFixture times out on CI; needs investigation.")]
	public void ElasticOpenTelemetry_Net90_ContainsElasticOpAmpClient()
	{
		AssertFixtureReady();
		var path = GetAssemblyPath("Elastic.OpenTelemetry", "release_net9.0");
		output.WriteLine($"Inspecting: {path}");
		Assert.True(File.Exists(path), $"Assembly not found: {path}");
		Assert.True(AssemblyHelper.ContainsType(path, "ElasticOpAmpClient"),
			"Elastic.OpenTelemetry net9.0 should contain ElasticOpAmpClient (source compiled in for NuGet)");
	}

	[SkipOnCiFact("BuildArtifactsFixture times out on CI; needs investigation.")]
	public void ElasticOpenTelemetry_Net90_DoesNotContainIsolatedLoadContext()
	{
		AssertFixtureReady();
		var path = GetAssemblyPath("Elastic.OpenTelemetry", "release_net9.0");
		output.WriteLine($"Inspecting: {path}");
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
