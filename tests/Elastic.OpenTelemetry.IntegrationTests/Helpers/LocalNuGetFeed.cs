// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using System.Text;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Elastic.OpenTelemetry.IntegrationTests.Helpers;

/// <summary>
/// Manages a temp directory as a local NuGet feed containing freshly packed
/// <c>.nupkg</c> files. Provides nuget.config generation for test consumer apps.
/// </summary>
internal sealed class LocalNuGetFeed : IDisposable
{
	private static readonly TimeSpan PostTimeoutLogDrain = TimeSpan.FromSeconds(30);

	/// <summary>Path to the temp directory containing <c>.nupkg</c> files.</summary>
	public string FeedPath { get; }

	private readonly string _buildScratchPath;

	public LocalNuGetFeed()
	{
		FeedPath = Path.Combine(Path.GetTempPath(), $"edot-nuget-feed-{Guid.NewGuid():N}");
		Directory.CreateDirectory(FeedPath);

		_buildScratchPath = Path.Combine(Path.GetTempPath(), $"edot-nuget-build-{Guid.NewGuid():N}");
		Directory.CreateDirectory(_buildScratchPath);
	}

	/// <summary>
	/// Packs a project and places the resulting <c>.nupkg</c> into this feed directory.
	/// </summary>
	/// <remarks>
	/// Uses isolated <c>BaseOutputPath</c> and <c>BaseIntermediateOutputPath</c> so that
	/// the restore + build performed by <c>dotnet pack</c> does not read or write the
	/// shared <c>.artifacts/</c> directory. This prevents interference with zip-distribution
	/// artifacts and avoids picking up a tainted <c>project.assets.json</c> left behind by
	/// an earlier <c>BuildingForZipDistribution=true</c> build.
	/// </remarks>
	public async Task PackProjectAsync(
		string projectPath,
		CancellationToken cancellationToken = default,
		IMessageSink? diagnosticMessageSink = null)
	{
		// Trailing separator is required by MSBuild for Base*Path properties.
		// Use forward slashes to avoid the Windows \"- escaping issue where a trailing
		// backslash before a closing quote is interpreted as an escaped literal quote,
		// merging adjacent arguments into a single mangled path.
		var binPath = Path.Combine(_buildScratchPath, "bin").Replace('\\', '/') + "/";
		var objPath = Path.Combine(_buildScratchPath, "obj").Replace('\\', '/') + "/";

		var arguments =
			$"pack \"{projectPath}\" -c Release -o \"{FeedPath}\" " +
			$"\"-p:BaseOutputPath={binPath}\" " +
			$"\"-p:BaseIntermediateOutputPath={objPath}\"";
		var command = $"dotnet {arguments}";
		WriteDiagnostic(diagnosticMessageSink, $"Running: {command}");

		var psi = new ProcessStartInfo
		{
			FileName = "dotnet",
			Arguments = arguments,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
		};

		using var process = Process.Start(psi)
			?? throw new InvalidOperationException($"Failed to start: {command}");

		using var outputDrainCts = new CancellationTokenSource();
		var stdout = new StringBuilder();
		var stderr = new StringBuilder();
		var stdoutTask = PumpOutputAsync(process.StandardOutput, stdout, outputDrainCts.Token);
		var stderrTask = PumpOutputAsync(process.StandardError, stderr, outputDrainCts.Token);

		try
		{
			await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
		{
			TryKillProcess(process, diagnosticMessageSink);
			outputDrainCts.CancelAfter(PostTimeoutLogDrain);
			await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
			if (outputDrainCts.IsCancellationRequested)
				WriteDiagnostic(diagnosticMessageSink,
					$"dotnet pack timed out; stopped waiting for additional output after {PostTimeoutLogDrain.TotalSeconds:F0}s.");
			WriteBufferedOutput(diagnosticMessageSink, stdout, stderr);

			throw new TimeoutException(
				$"dotnet pack timed out for project: {projectPath}.\n" +
				$"stdout:\n{stdout}\nstderr:\n{stderr}", ex);
		}

		await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);

		if (process.ExitCode != 0)
		{
			WriteBufferedOutput(diagnosticMessageSink, stdout, stderr);
			throw new InvalidOperationException(
				$"dotnet pack failed (exit code {process.ExitCode}).\n" +
				$"stdout:\n{stdout}\nstderr:\n{stderr}");
		}

		WriteDiagnostic(diagnosticMessageSink, $"dotnet pack completed successfully for '{projectPath}'.");
	}

	private static async Task PumpOutputAsync(
		StreamReader reader,
		StringBuilder sink,
		CancellationToken cancellationToken)
	{
		try
		{
			while (true)
			{
				var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
				if (line is null)
					break;

				sink.AppendLine(line);
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
	}

	private static void WriteBufferedOutput(IMessageSink? diagnosticMessageSink, StringBuilder stdout, StringBuilder stderr)
	{
		if (stdout.Length > 0)
			WriteDiagnostic(diagnosticMessageSink, $"dotnet pack stdout:\n{stdout}");

		if (stderr.Length > 0)
			WriteDiagnostic(diagnosticMessageSink, $"dotnet pack stderr:\n{stderr}");
	}

	private static void TryKillProcess(Process process, IMessageSink? diagnosticMessageSink)
	{
		try
		{
			if (!process.HasExited)
			{
				process.Kill(entireProcessTree: true);
				WriteDiagnostic(diagnosticMessageSink, "dotnet pack cancelled; killed process tree.");
			}
		}
		catch (Exception ex)
		{
			WriteDiagnostic(diagnosticMessageSink, $"Failed to kill timed-out dotnet pack process: {ex.Message}");
		}
	}

	private static void WriteDiagnostic(IMessageSink? diagnosticMessageSink, string message) =>
		diagnosticMessageSink?.OnMessage(new DiagnosticMessage(message));

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

	/// <summary>Cleans up the temp feed and build scratch directories.</summary>
	public void Dispose()
	{
		TryDeleteDirectory(FeedPath);
		TryDeleteDirectory(_buildScratchPath);
	}

	private static void TryDeleteDirectory(string path)
	{
		if (Directory.Exists(path))
		{
			try
			{ Directory.Delete(path, true); }
			catch
			{
				// Best-effort cleanup
			}
		}
	}
}
