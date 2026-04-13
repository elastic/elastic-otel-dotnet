// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using Xunit.Abstractions;

namespace Elastic.OpenTelemetry.BuildVerification.Tests.Helpers;

/// <summary>
/// Shared fixture for the "BuildArtifacts" collection. Runs all expensive
/// dotnet build / pack operations once, then exposes their outputs to
/// <see cref="AssemblyTypeInspectionTests"/> and <see cref="NuGetPackageMetadataTests"/>.
/// </summary>
public class BuildArtifactsFixture : IAsyncLifetime, IDisposable
{
	private const string AutoInstProject =
		"src/Elastic.OpenTelemetry.AutoInstrumentation/Elastic.OpenTelemetry.AutoInstrumentation.csproj";

	private const string ElasticOTelProject =
		"src/Elastic.OpenTelemetry/Elastic.OpenTelemetry.csproj";

	private readonly IMessageSink _diagnosticSink;

	/// <summary>Directory where packed .nupkg files are written.</summary>
	public string PackOutputDir { get; } = Path.Combine(
		Path.GetTempPath(), $"edot-pack-test-{Guid.NewGuid():N}"[..30]);

	/// <summary>Non-null error message if fixture setup failed.</summary>
	public string? InitializationError { get; private set; }

	public BuildArtifactsFixture(IMessageSink diagnosticSink) =>
		_diagnosticSink = diagnosticSink;

	public async Task InitializeAsync()
	{
		var fixtureTimer = Stopwatch.StartNew();
		Log("BuildArtifactsFixture starting — 4 builds + 2 packs");

		// Overall budget for all builds + packs. Prevents indefinite hangs on CI.
		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(20));

		try
		{
			// ── Builds (for AssemblyTypeInspectionTests) ─────────────────
			// Each targets a different project + TFM + property combo, writing
			// to separate output directories under .artifacts/bin/, so they are
			// safe to run concurrently.
			var buildSpecs = new (string Project, Dictionary<string, string> Props)[]
			{
				(AutoInstProject, new() { ["TargetFramework"] = "net8.0", ["BuildingForZipDistribution"] = "true" }),
				(AutoInstProject, new() { ["TargetFramework"] = "net462", ["BuildingForZipDistribution"] = "true" }),
				(ElasticOTelProject, new() { ["TargetFramework"] = "net8.0" }),
				(ElasticOTelProject, new() { ["TargetFramework"] = "net9.0" })
			};

			var buildTasks = buildSpecs.Select(async spec =>
			{
				var label = $"{Path.GetFileNameWithoutExtension(spec.Project)} [{spec.Props.GetValueOrDefault("TargetFramework", "?")}]";
				Log($"  Build starting: {label}");
				var sw = Stopwatch.StartNew();
				var result = await DotNetHelper.BuildAsync(spec.Project, spec.Props).ConfigureAwait(false);
				Log($"  Build finished: {label} — {sw.Elapsed.TotalSeconds:F1}s, exit={result.ExitCode}");
				return (label, result);
			}).ToArray();

			var buildResults = await Task.WhenAll(buildTasks).ConfigureAwait(false);
			cts.Token.ThrowIfCancellationRequested();

			foreach (var (label, result) in buildResults)
			{
				Assert.True(result.ExitCode == 0,
					$"Build failed ({label}):\n{result.Error}\n{result.Output}");
			}

			Log($"All builds completed in {fixtureTimer.Elapsed.TotalSeconds:F1}s");

			// ── Packs (for NuGetPackageMetadataTests) ───────────────────
			// Packs use isolated scratch directories, so they are safe to run
			// concurrently with each other.
			Directory.CreateDirectory(PackOutputDir);

			var packSpecs = new[]
			{
				(Project: ElasticOTelProject, Label: "Elastic.OpenTelemetry"),
				(Project: AutoInstProject, Label: "AutoInstrumentation")
			};

			var packTasks = packSpecs.Select(async spec =>
			{
				Log($"  Pack starting: {spec.Label}");
				var sw = Stopwatch.StartNew();
				var result = await DotNetHelper.PackAsync(spec.Project, PackOutputDir).ConfigureAwait(false);
				Log($"  Pack finished: {spec.Label} — {sw.Elapsed.TotalSeconds:F1}s, exit={result.ExitCode}");
				return (spec.Label, result);
			}).ToArray();

			var packResults = await Task.WhenAll(packTasks).ConfigureAwait(false);
			cts.Token.ThrowIfCancellationRequested();

			foreach (var (label, result) in packResults)
			{
				Assert.True(result.ExitCode == 0,
					$"Pack failed ({label}):\n{result.Error}\n{result.Output}");
			}

			Log($"BuildArtifactsFixture completed in {fixtureTimer.Elapsed.TotalSeconds:F1}s");
		}
		catch (OperationCanceledException)
		{
			InitializationError = $"BuildArtifactsFixture timed out after {fixtureTimer.Elapsed.TotalSeconds:F0}s (20 min budget).";
			Log(InitializationError);
			throw new TimeoutException(InitializationError);
		}
		catch (Exception ex)
		{
			InitializationError = ex.Message;
			Log($"BuildArtifactsFixture failed after {fixtureTimer.Elapsed.TotalSeconds:F1}s: {ex.Message}");
			throw;
		}
	}

	public Task DisposeAsync()
	{
		Dispose();
		return Task.CompletedTask;
	}

	public void Dispose()
	{
		if (Directory.Exists(PackOutputDir))
		{
			try
			{ Directory.Delete(PackOutputDir, true); }
			catch { /* best-effort cleanup */ }
		}
	}

	private void Log(string message) =>
		_diagnosticSink.OnMessage(new Xunit.Sdk.DiagnosticMessage($"[BV-Fixture] {message}"));
}
