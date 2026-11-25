// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Elastic.OpenTelemetry.BuildVerification.Tests.Helpers;

internal static class DotNetHelper
{
	private static readonly TimeSpan OutputDrainTimeout = TimeSpan.FromSeconds(30);

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
		Dictionary<string, string>? properties = null,
		CancellationToken cancellationToken = default)
	{
		var args = new List<string> { "build", GetProjectPath(projectRelativePath), "-c", "Release" };
		AddProperties(args, properties);
		return await RunDotNetAsync(args, cancellationToken: cancellationToken).ConfigureAwait(false);
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
		Dictionary<string, string>? properties = null,
		CancellationToken cancellationToken = default)
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
			return await RunDotNetAsync(args, TimeSpan.FromMinutes(10), cancellationToken).ConfigureAwait(false);
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

	internal static async Task<DotNetResult> RunDotNetAsync(
		List<string> args, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		timeout ??= TimeSpan.FromMinutes(5);
		using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		cts.CancelAfter(timeout.Value);

		var logDirectory = Path.Combine(SolutionRoot, ".artifacts", "tests", "build-verification");
		Directory.CreateDirectory(logDirectory);
		args.Add($"-bl:{Path.Combine(logDirectory, "{}.binlog")}");

		var psi = new ProcessStartInfo("dotnet")
		{
			WorkingDirectory = SolutionRoot,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true
		};
		psi.Environment["MSBUILDDISABLENODEREUSE"] = "1";
		foreach (var arg in args)
			psi.ArgumentList.Add(arg);

		using var process = Process.Start(psi)!;

		using var outputDrainCts = new CancellationTokenSource();
		var output = new StringBuilder();
		var error = new StringBuilder();
		var outputTask = PumpOutputAsync(process.StandardOutput, output, outputDrainCts.Token);
		var errorTask = PumpOutputAsync(process.StandardError, error, outputDrainCts.Token);

		var timedOut = false;
		try
		{
			await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (cts.IsCancellationRequested)
		{
			timedOut = true;
			try
			{ process.Kill(entireProcessTree: true); }
			catch { /* best effort */ }
		}
		finally
		{
			// Descendant build workers can retain the pipes even after dotnet exits.
			outputDrainCts.CancelAfter(OutputDrainTimeout);
			await Task.WhenAll(outputTask, errorTask).ConfigureAwait(false);
		}

		if (outputDrainCts.IsCancellationRequested)
			error.AppendLine($"Output pipes remained open; stopped draining after {OutputDrainTimeout.TotalSeconds:0} seconds.");

		if (cancellationToken.IsCancellationRequested)
			throw new OperationCanceledException(
				$"dotnet {args[0]} was cancelled.\n{output}\n{error}", cancellationToken);

		if (timedOut)
			return new DotNetResult(-1, output.ToString(),
				$"Process timed out after {timeout.Value.TotalMinutes:0} minutes.\n{error}");

		return new DotNetResult(process.ExitCode, output.ToString(), error.ToString());
	}

	private static async Task PumpOutputAsync(
		StreamReader reader, StringBuilder sink, CancellationToken cancellationToken)
	{
		try
		{
			while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
				sink.AppendLine(line);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
	}
}

internal sealed record DotNetResult(int ExitCode, string Output, string Error);
