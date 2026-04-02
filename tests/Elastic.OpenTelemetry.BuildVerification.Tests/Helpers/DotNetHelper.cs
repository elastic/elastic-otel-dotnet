// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using System.Text.Json;

namespace Elastic.OpenTelemetry.BuildVerification.Tests.Helpers;

internal static class DotNetHelper
{
	internal static string SolutionRoot { get; } = FindSolutionRoot();

	private static string FindSolutionRoot()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir is not null)
		{
			if (File.Exists(Path.Combine(dir.FullName, "build.bat")))
				return dir.FullName;
			dir = dir.Parent;
		}
		throw new InvalidOperationException("Could not find solution root (looking for build.bat)");
	}

	internal static string GetProjectPath(string relativePath) =>
		Path.Combine(SolutionRoot, relativePath);

	/// <summary>
	/// Evaluates an MSBuild property without building. Very fast.
	/// </summary>
	internal static async Task<string> EvaluatePropertyAsync(
		string projectRelativePath,
		string propertyName,
		Dictionary<string, string>? properties = null)
	{
		var args = BuildMsBuildArgs(projectRelativePath, properties);
		args.Add($"-getProperty:{propertyName}");

		var result = await RunDotNetAsync(args);
		Assert.True(result.ExitCode == 0,
			$"MSBuild evaluation failed for {projectRelativePath}:\n{result.Error}\n{result.Output}");

		// -getProperty returns the raw property value as plain text
		return result.Output.Trim();
	}

	/// <summary>
	/// Evaluates MSBuild items without building. Returns item Identity values.
	/// </summary>
	internal static async Task<List<string>> EvaluateItemsAsync(
		string projectRelativePath,
		string itemType,
		Dictionary<string, string>? properties = null)
	{
		var args = BuildMsBuildArgs(projectRelativePath, properties);
		args.Add($"-getItem:{itemType}");

		var result = await RunDotNetAsync(args);
		Assert.True(result.ExitCode == 0,
			$"MSBuild evaluation failed for {projectRelativePath}:\n{result.Error}\n{result.Output}");

		using var json = JsonDocument.Parse(result.Output.Trim());
		var items = json.RootElement.GetProperty("Items");

		if (!items.TryGetProperty(itemType, out var itemArray))
			return [];

		return itemArray.EnumerateArray()
			.Select(item => item.GetProperty("Identity").GetString() ?? string.Empty)
			.ToList();
	}

	/// <summary>
	/// Runs dotnet build with the specified properties.
	/// </summary>
	internal static async Task<DotNetResult> BuildAsync(
		string projectRelativePath,
		Dictionary<string, string>? properties = null)
	{
		var args = new List<string> { "build", GetProjectPath(projectRelativePath), "-c", "Release" };
		AddProperties(args, properties);
		return await RunDotNetAsync(args);
	}

	/// <summary>
	/// Runs dotnet pack with output redirected to the specified directory.
	/// </summary>
	/// <remarks>
	/// Uses isolated <c>BaseOutputPath</c> and <c>BaseIntermediateOutputPath</c> so that the
	/// pack performs a full restore + build into a scratch directory instead of reading from
	/// the shared <c>.artifacts/</c> tree. This prevents picking up a tainted
	/// <c>project.assets.json</c> left behind by a <c>BuildingForZipDistribution=true</c> build.
	/// </remarks>
	internal static async Task<DotNetResult> PackAsync(
		string projectRelativePath,
		string outputDirectory,
		Dictionary<string, string>? properties = null)
	{
		var scratchDir = Path.Combine(Path.GetTempPath(), $"edot-bv-pack-{Guid.NewGuid():N}");
		try
		{
			// Trailing separator is required by MSBuild for Base*Path properties.
			// Use forward slashes to avoid the Windows \"- escaping issue where a trailing
			// backslash before a closing quote is interpreted as an escaped literal quote,
			// merging adjacent arguments into a single mangled path.
			var binPath = Path.Combine(scratchDir, "bin").Replace('\\', '/') + "/";
			var objPath = Path.Combine(scratchDir, "obj").Replace('\\', '/') + "/";

			var args = new List<string>
			{
				"pack", GetProjectPath(projectRelativePath),
				"-c", "Release",
				"--output", outputDirectory,
				$"-p:BaseOutputPath={binPath}",
				$"-p:BaseIntermediateOutputPath={objPath}"
			};
			AddProperties(args, properties);
			return await RunDotNetAsync(args, TimeSpan.FromMinutes(10));
		}
		finally
		{
			if (Directory.Exists(scratchDir))
			{
				try
				{ Directory.Delete(scratchDir, true); }
				catch { /* best-effort cleanup */ }
			}
		}
	}

	private static List<string> BuildMsBuildArgs(
		string projectRelativePath,
		Dictionary<string, string>? properties)
	{
		var args = new List<string> { "msbuild", GetProjectPath(projectRelativePath) };
		AddProperties(args, properties);
		return args;
	}

	private static void AddProperties(List<string> args, Dictionary<string, string>? properties)
	{
		if (properties is null)
			return;
		foreach (var (key, value) in properties)
			args.Add($"-p:{key}={value}");
	}

	private static async Task<DotNetResult> RunDotNetAsync(List<string> args, TimeSpan? timeout = null)
	{
		timeout ??= TimeSpan.FromMinutes(5);
		using var cts = new CancellationTokenSource(timeout.Value);

		var psi = new ProcessStartInfo("dotnet")
		{
			WorkingDirectory = SolutionRoot,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true
		};
		foreach (var arg in args)
			psi.ArgumentList.Add(arg);

		using var process = Process.Start(psi)!;

		// Read both streams concurrently to avoid deadlock when either buffer fills
		var outputTask = process.StandardOutput.ReadToEndAsync();
		var errorTask = process.StandardError.ReadToEndAsync();

		try
		{
			await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			try
			{ process.Kill(entireProcessTree: true); }
			catch { /* best effort */ }

			var partialOut = await outputTask.ConfigureAwait(false);
			var partialErr = await errorTask.ConfigureAwait(false);
			return new DotNetResult(-1, partialOut,
				$"Process timed out after {timeout.Value.TotalMinutes:0} minutes.\n{partialErr}");
		}

		var output = await outputTask.ConfigureAwait(false);
		var error = await errorTask.ConfigureAwait(false);

		return new DotNetResult(process.ExitCode, output, error);
	}
}

internal sealed record DotNetResult(int ExitCode, string Output, string Error);
