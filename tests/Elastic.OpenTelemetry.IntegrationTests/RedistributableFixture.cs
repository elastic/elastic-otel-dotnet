// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Elastic.OpenTelemetry.IntegrationTests;

/// <summary>
/// xUnit collection fixture that finds or builds the EDOT redistributable zip,
/// extracts the platform-appropriate archive, and provides the installation
/// directory for auto-instrumentation tests.
/// </summary>
/// <remarks>
/// <para>Expects the redistributable zip to already exist in
/// <c>.artifacts/elastic-distribution/</c>. The fixture is a consumer, not a builder.</para>
/// <para>Before running these tests, first run <c>./build.sh redistribute</c>
/// to produce the zip, then run the integration tests separately.</para>
/// </remarks>
public class RedistributableFixture : IAsyncLifetime
{
	private readonly IMessageSink _diagnosticMessageSink;
	private string? _extractionDirectory;
	private string? _publishDirectory;

	public RedistributableFixture(IMessageSink diagnosticMessageSink) => _diagnosticMessageSink = diagnosticMessageSink;

	/// <summary>
	/// Path to the extracted redistributable — mimics a real installation.
	/// Use with <see cref="Helpers.ProfilerEnvironment.ForCoreCLR"/> to generate profiler env vars.
	/// </summary>
	public string InstallationDirectory { get; private set; } = string.Empty;

	/// <summary>
	/// Path to the published AutoInstr.Console.Net8.dll, ready to run under the profiler.
	/// </summary>
	public string Net8AppPath { get; private set; } = string.Empty;

	/// <summary>Whether the fixture initialized successfully.</summary>
	public bool IsReady { get; private set; }

	/// <summary>Error message if initialization failed.</summary>
	public string? InitializationError { get; private set; }

	public async Task InitializeAsync()
	{
		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
		var ct = cts.Token;

		try
		{
			var solutionRoot = FindSolutionRoot();
			const string redistributeHelp = "Run './build.sh redistribute' to build the redistributable zips first.";

			// 1. Find the redistributable zip (build system ensures freshness)
			var zipPath = FindDistributionZip(solutionRoot)
				?? throw new FileNotFoundException(
					"Redistributable zip not found. " +
					redistributeHelp + " " +
					"Then rerun the integration tests.\n" +
					$"Expected: elastic-dotnet-instrumentation-{GetPlatformZipSuffix()}.zip " +
					$"under {Path.Combine(solutionRoot, ".artifacts", "elastic-distribution")}");

			// 2. Extract to a temp directory
			_extractionDirectory = Path.Combine(
				Path.GetTempPath(), $"edot-redist-{Guid.NewGuid():N}");
			ZipFile.ExtractToDirectory(zipPath, _extractionDirectory);
			InstallationDirectory = _extractionDirectory;

			// 3. Build + publish the auto-instrumentation test app
			var appProjectPath = Path.Combine(solutionRoot,
				"test-applications", "AutoInstr.Console.Net8", "AutoInstr.Console.Net8.csproj");
			_publishDirectory = Path.Combine(
				Path.GetTempPath(), $"edot-autoinstr-net8-{Guid.NewGuid():N}");
			var publishDir = _publishDirectory;

			await RunDotnetAsync(
				$"publish \"{appProjectPath}\" -c Release -o \"{publishDir}\"", ct).ConfigureAwait(false);

			Net8AppPath = Path.Combine(publishDir, "AutoInstr.Console.Net8.dll");

			if (!File.Exists(Net8AppPath))
				throw new FileNotFoundException(
					$"Published app not found at expected path: {Net8AppPath}");

			IsReady = true;
		}
		catch (Exception ex)
		{
			InitializationError = ex.ToString();
			WriteFixtureLog($"Initialization failed: {InitializationError}");
		}
	}

	public Task DisposeAsync()
	{
		string?[] dirs = [_extractionDirectory, _publishDirectory];

		foreach (var dir in dirs)
		{
			if (dir is not null && Directory.Exists(dir))
			{
				try
				{ Directory.Delete(dir, true); }
				catch
				{
					// Best-effort cleanup
				}
			}
		}

		return Task.CompletedTask;
	}

	private static string? FindDistributionZip(string solutionRoot)
	{
		var distDir = Path.Combine(solutionRoot, ".artifacts", "elastic-distribution");

		if (!Directory.Exists(distDir))
			return null;

		var zipName = $"elastic-dotnet-instrumentation-{GetPlatformZipSuffix()}.zip";
		return Directory.GetFiles(distDir, zipName, SearchOption.AllDirectories)
			.OrderByDescending(File.GetLastWriteTimeUtc)
			.FirstOrDefault();
	}

	private static string GetPlatformZipSuffix()
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			return "windows";
		if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			return "macos";

		// Linux: glibc vs musl (Alpine), x64 vs arm64
		// Use the same detection as build/patch-dotnet-auto-install.sh: ldd /bin/ls | grep musl
		var arch = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
		var libc = IsMusl() ? "musl" : "glibc";
		return $"linux-{libc}-{arch}";
	}

	private static bool IsMusl()
	{
		try
		{
			var psi = new ProcessStartInfo
			{
				FileName = "ldd",
				Arguments = "/bin/ls",
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			};
			using var proc = Process.Start(psi);
			if (proc is null)
				return false;
			var output = proc.StandardOutput.ReadToEnd();
			proc.WaitForExit();
			return output.Contains("musl", StringComparison.OrdinalIgnoreCase);
		}
		catch
		{
			return false;
		}
	}

	private void WriteFixtureLog(string message) =>
		_diagnosticMessageSink.OnMessage(
			new DiagnosticMessage($"[{DateTimeOffset.UtcNow:O}] [RedistributableFixture] {message}"));

	private Task RunDotnetAsync(string arguments, CancellationToken ct) =>
		NuGetPackageFixture.RunDotnetWithLoggingAsync(arguments, WriteFixtureLog, ct);

	private static string FindSolutionRoot()
	{
		var dir = AppContext.BaseDirectory;
		while (dir is not null)
		{
			if (Directory.EnumerateFiles(dir, "*.slnx").Any()
				|| Directory.EnumerateFiles(dir, "*.sln").Any())
				return dir;

			dir = Directory.GetParent(dir)?.FullName;
		}

		throw new InvalidOperationException(
			"Could not find solution root from " + AppContext.BaseDirectory);
	}
}

[CollectionDefinition("Redistributable")]
public class RedistributableCollection : ICollectionFixture<RedistributableFixture>;
