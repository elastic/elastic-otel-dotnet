// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;

namespace Elastic.OpenTelemetry.IntegrationTests.Helpers;

/// <summary>
/// Manages a temp directory as a local NuGet feed containing freshly packed
/// <c>.nupkg</c> files. Provides nuget.config generation for test consumer apps.
/// </summary>
internal sealed class LocalNuGetFeed : IDisposable
{
	/// <summary>Path to the temp directory containing <c>.nupkg</c> files.</summary>
	public string FeedPath { get; }

	public LocalNuGetFeed()
	{
		FeedPath = Path.Combine(Path.GetTempPath(), $"edot-nuget-feed-{Guid.NewGuid():N}");
		Directory.CreateDirectory(FeedPath);
	}

	/// <summary>
	/// Packs a project and places the resulting <c>.nupkg</c> into this feed directory.
	/// </summary>
	public async Task PackProjectAsync(string projectPath, CancellationToken cancellationToken = default)
	{
		var psi = new ProcessStartInfo
		{
			FileName = "dotnet",
			Arguments = $"pack \"{projectPath}\" -c Release -o \"{FeedPath}\"",
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
		};

		using var process = Process.Start(psi)
			?? throw new InvalidOperationException($"Failed to start: dotnet pack \"{projectPath}\"");

		// Read both streams concurrently to avoid deadlock when either buffer fills
		var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
		var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
		await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
		var stdout = await stdoutTask;
		var stderr = await stderrTask;

		if (process.ExitCode != 0)
			throw new InvalidOperationException(
				$"dotnet pack failed (exit code {process.ExitCode}).\n" +
				$"stdout:\n{stdout}\nstderr:\n{stderr}");
	}

	/// <summary>
	/// Determines the version of a packed package by inspecting <c>.nupkg</c> filenames.
	/// Returns <c>null</c> if the package is not found in the feed.
	/// </summary>
	public string? GetPackageVersion(string packageId)
	{
		var files = Directory.GetFiles(FeedPath, $"{packageId}.*.nupkg");
		if (files.Length == 0)
			return null;

		// Filename format: {PackageId}.{Version}.nupkg
		var fileName = Path.GetFileNameWithoutExtension(files[0]);
		return fileName[(packageId.Length + 1)..];
	}

	/// <summary>
	/// Generates a <c>nuget.config</c> that points to this local feed (highest priority)
	/// plus <c>nuget.org</c> for transitive dependencies. Writes to the specified directory.
	/// </summary>
	public void WriteNuGetConfig(string targetDirectory)
	{
		var configContent = $"""
			<?xml version="1.0" encoding="utf-8"?>
			<configuration>
			  <packageSources>
			    <clear />
			    <add key="local-edot" value="{FeedPath}" />
			    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
			  </packageSources>
			</configuration>
			""";

		Directory.CreateDirectory(targetDirectory);
		File.WriteAllText(Path.Combine(targetDirectory, "nuget.config"), configContent);
	}

	/// <summary>Cleans up the temp feed directory.</summary>
	public void Dispose()
	{
		if (Directory.Exists(FeedPath))
		{
			try
			{ Directory.Delete(FeedPath, true); }
			catch
			{
				// Best-effort cleanup
			}
		}
	}
}
