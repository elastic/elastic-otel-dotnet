// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.BuildVerification.Tests.Helpers;

namespace Elastic.OpenTelemetry.BuildVerification.Tests;

/// <summary>
/// Verifies that MSBuild conditions produce the correct properties and items
/// for each build scenario. These tests use MSBuild evaluation only (no actual build)
/// and are very fast.
/// </summary>
public class MsBuildEvaluationTests
{
	private const string ElasticOTelProject =
		"src/Elastic.OpenTelemetry/Elastic.OpenTelemetry.csproj";

	private const string AutoInstrumentationProject =
		"src/Elastic.OpenTelemetry.AutoInstrumentation/Elastic.OpenTelemetry.AutoInstrumentation.csproj";

	private const string CoreProject =
		"src/Elastic.OpenTelemetry.Core/Elastic.OpenTelemetry.Core.csproj";

	private const string OpAmpProject =
		"src/Elastic.OpenTelemetry.OpAmp/Elastic.OpenTelemetry.OpAmp.csproj";

	private const string UseIsolatedOpAmpClient = "USE_ISOLATED_OPAMP_CLIENT";
	private const string SkipSolutionBuildImported = "SkipSolutionBuildImported";

	// ── Elastic.OpenTelemetry (NuGet package) ─────────────────────────────

	[Theory]
	[InlineData("netstandard2.0", "Release")]
	[InlineData("netstandard2.1", "Release")]
	[InlineData("net462", "Release")]
	[InlineData("net8.0", "Release")]
	[InlineData("net9.0", "Release")]
	[InlineData("net8.0", "Debug")]
	[InlineData("net9.0", "Debug")]
	public async Task ElasticOpenTelemetry_NeverDefines_UseIsolatedOpAmpClient(
		string tfm, string configuration)
	{
		var defines = await DotNetHelper.EvaluatePropertyAsync(
			ElasticOTelProject,
			"DefineConstants",
			new() { ["TargetFramework"] = tfm, ["Configuration"] = configuration });

		Assert.DoesNotContain(UseIsolatedOpAmpClient, defines);
	}

	[Theory]
	[InlineData("netstandard2.0")]
	[InlineData("netstandard2.1")]
	[InlineData("net462")]
	[InlineData("net8.0")]
	[InlineData("net9.0")]
	public async Task ElasticOpenTelemetry_AlwaysCompilesOpAmpSource(string tfm)
	{
		var compileItems = await DotNetHelper.EvaluateItemsAsync(
			ElasticOTelProject,
			"Compile",
			new() { ["TargetFramework"] = tfm });

		Assert.Contains(compileItems,
			item => item.Contains("Elastic.OpenTelemetry.OpAmp.Abstractions"));

		// Match the OpAmp impl directory without matching OpAmp.Abstractions
		var opAmpImplSeparator = $"Elastic.OpenTelemetry.OpAmp{Path.DirectorySeparatorChar}";
		Assert.Contains(compileItems, item => item.Contains(opAmpImplSeparator));
	}

	[Theory]
	[InlineData("netstandard2.0")]
	[InlineData("netstandard2.1")]
	[InlineData("net462")]
	[InlineData("net8.0")]
	[InlineData("net9.0")]
	public async Task ElasticOpenTelemetry_HasNoOpAmpProjectReferences(string tfm)
	{
		var projectRefs = await DotNetHelper.EvaluateItemsAsync(
			ElasticOTelProject,
			"ProjectReference",
			new() { ["TargetFramework"] = tfm });

		Assert.DoesNotContain(projectRefs, item => item.Contains("OpAmp"));
	}

	// ── AutoInstrumentation — NuGet (no BuildingForZipDistribution) ───────

	[Theory]
	[InlineData("net8.0")]
	[InlineData("net462")]
	public async Task AutoInstrumentation_NuGet_DoesNotDefine_UseIsolatedOpAmpClient(string tfm)
	{
		var defines = await DotNetHelper.EvaluatePropertyAsync(
			AutoInstrumentationProject,
			"DefineConstants",
			new() { ["TargetFramework"] = tfm, ["Configuration"] = "Release" });

		Assert.DoesNotContain(UseIsolatedOpAmpClient, defines);
	}

	[Theory]
	[InlineData("net8.0")]
	[InlineData("net462")]
	public async Task AutoInstrumentation_NuGet_CompilesOpAmpSource(string tfm)
	{
		var props = new Dictionary<string, string> { ["TargetFramework"] = tfm };

		var compileItems = await DotNetHelper.EvaluateItemsAsync(
			AutoInstrumentationProject, "Compile", props);

		Assert.Contains(compileItems,
			item => item.Contains("Elastic.OpenTelemetry.OpAmp.Abstractions"));

		var opAmpImplSeparator = $"Elastic.OpenTelemetry.OpAmp{Path.DirectorySeparatorChar}";
		Assert.Contains(compileItems, item => item.Contains(opAmpImplSeparator));

		var projectRefs = await DotNetHelper.EvaluateItemsAsync(
			AutoInstrumentationProject, "ProjectReference", props);
		Assert.DoesNotContain(projectRefs, item => item.Contains("OpAmp"));
	}

	// ── AutoInstrumentation — Zip + net8.0 (ALC isolation) ────────────────

	[Fact]
	public async Task AutoInstrumentation_Zip_Net80_Defines_UseIsolatedOpAmpClient()
	{
		var defines = await DotNetHelper.EvaluatePropertyAsync(
			AutoInstrumentationProject,
			"DefineConstants",
			new()
			{
				["TargetFramework"] = "net8.0",
				["Configuration"] = "Release",
				["BuildingForZipDistribution"] = "true"
			});

		Assert.Contains(UseIsolatedOpAmpClient, defines);
	}

	[Fact]
	public async Task AutoInstrumentation_Zip_Net80_UsesProjectReferences_NotCompiledSource()
	{
		var props = new Dictionary<string, string>
		{
			["TargetFramework"] = "net8.0",
			["BuildingForZipDistribution"] = "true"
		};

		// Should have ProjectReferences to OpAmp projects
		var projectRefs = await DotNetHelper.EvaluateItemsAsync(
			AutoInstrumentationProject, "ProjectReference", props);

		Assert.Contains(projectRefs,
			item => item.Contains("Elastic.OpenTelemetry.OpAmp.Abstractions"));
		Assert.Contains(projectRefs,
			item => item.Contains("Elastic.OpenTelemetry.OpAmp.csproj"));

		// Should NOT have OpAmp source compiled in
		var compileItems = await DotNetHelper.EvaluateItemsAsync(
			AutoInstrumentationProject, "Compile", props);

		var opAmpImplSeparator = $"Elastic.OpenTelemetry.OpAmp{Path.DirectorySeparatorChar}";
		Assert.DoesNotContain(compileItems, item => item.Contains(opAmpImplSeparator));
		Assert.DoesNotContain(compileItems,
			item => item.Contains($"Elastic.OpenTelemetry.OpAmp.Abstractions{Path.DirectorySeparatorChar}"));
	}

	// ── AutoInstrumentation — Zip + net462 (source compiled in) ───────────

	[Fact]
	public async Task AutoInstrumentation_Zip_Net462_DoesNotDefine_UseIsolatedOpAmpClient()
	{
		var defines = await DotNetHelper.EvaluatePropertyAsync(
			AutoInstrumentationProject,
			"DefineConstants",
			new()
			{
				["TargetFramework"] = "net462",
				["Configuration"] = "Release",
				["BuildingForZipDistribution"] = "true"
			});

		Assert.DoesNotContain(UseIsolatedOpAmpClient, defines);
	}

	[Fact]
	public async Task AutoInstrumentation_Zip_Net462_CompilesOpAmpSource_NoProjectReferences()
	{
		var props = new Dictionary<string, string>
		{
			["TargetFramework"] = "net462",
			["BuildingForZipDistribution"] = "true"
		};

		// Should have OpAmp source compiled in
		var compileItems = await DotNetHelper.EvaluateItemsAsync(
			AutoInstrumentationProject, "Compile", props);

		Assert.Contains(compileItems,
			item => item.Contains("Elastic.OpenTelemetry.OpAmp.Abstractions"));

		var opAmpImplSeparator = $"Elastic.OpenTelemetry.OpAmp{Path.DirectorySeparatorChar}";
		Assert.Contains(compileItems, item => item.Contains(opAmpImplSeparator));

		// Should NOT have ProjectReferences to OpAmp
		var projectRefs = await DotNetHelper.EvaluateItemsAsync(
			AutoInstrumentationProject, "ProjectReference", props);
		Assert.DoesNotContain(projectRefs, item => item.Contains("OpAmp"));
	}

	// ── BLD-002: Solution-build skip signal ──────────────────────────────

	[Fact]
	public async Task CoreProject_SolutionBuild_SkipBehaviorIsActive()
	{
		var value = await DotNetHelper.EvaluatePropertyAsync(
			CoreProject,
			SkipSolutionBuildImported,
			new() { ["SolutionPath"] = "Elastic.OpenTelemetry.slnx" });

		Assert.Equal("true", value);
	}

	[Fact]
	public async Task CoreProject_ProjectBuild_SkipBehaviorIsInactive()
	{
		var value = await DotNetHelper.EvaluatePropertyAsync(
			CoreProject,
			SkipSolutionBuildImported);

		Assert.Empty(value);
	}

	[Fact]
	public async Task NonSourceOnlyProject_SolutionBuild_CompilesNormally()
	{
		var value = await DotNetHelper.EvaluatePropertyAsync(
			OpAmpProject,
			SkipSolutionBuildImported,
			new() { ["SolutionPath"] = "Elastic.OpenTelemetry.slnx" });

		Assert.Empty(value);
	}

	[Fact]
	public void SignalConsistency_SolutionPathIsCanonical()
	{
		var targetsPath = DotNetHelper.GetProjectPath("Directory.Build.targets");
		var content = File.ReadAllText(targetsPath);

		Assert.Contains("$(SolutionPath)", content);
		Assert.DoesNotContain("$(IsSolutionBuild)", content);
	}

	[Fact]
	public void NoRemnantIsSolutionBuild()
	{
		var root = DotNetHelper.SolutionRoot;
		var patterns = new[] { "*.props", "*.targets", "*.csproj" };

		var files = patterns
			.SelectMany(p => Directory.EnumerateFiles(root, p, SearchOption.AllDirectories))
			.ToList();

		foreach (var file in files)
		{
			var content = File.ReadAllText(file);
			Assert.False(
				content.Contains("IsSolutionBuild"),
				$"Found remnant 'IsSolutionBuild' in {Path.GetRelativePath(root, file)}");
		}
	}
}
