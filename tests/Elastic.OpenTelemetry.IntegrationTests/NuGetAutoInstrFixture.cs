// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.IO.Compression;
using System.Runtime.InteropServices;
using Elastic.OpenTelemetry.IntegrationTests.Helpers;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Elastic.OpenTelemetry.IntegrationTests;

/// <summary>
/// xUnit collection fixture for testing the <c>Elastic.OpenTelemetry.AutoInstrumentation</c>
/// NuGet package consumed via PackageReference. Combines two concerns:
/// <list type="bullet">
///   <item>Packs the AutoInstrumentation NuGet and publishes consumer apps against it</item>
///   <item>Finds the redistributable zip for profiler infrastructure (native DLLs, startup hooks)</item>
/// </list>
/// </summary>
/// <remarks>
/// <para>The consumer apps reference the packed <c>.nupkg</c>, so the plugin DLL comes from
/// NuGet (in the app's output directory), not from the zip's <c>net/</c> folder. The
/// redistributable zip only provides the profiler infrastructure.</para>
/// <para>Before running these tests, first run <c>./build.sh redistribute</c>
/// to produce the profiler infrastructure zip. This fixture packs the NuGet
/// artifacts it needs during initialization.</para>
/// </remarks>
public class NuGetAutoInstrFixture : IAsyncLifetime
{
	private static readonly TimeSpan SharedSetupTimeout = TimeSpan.FromMinutes(8);
	private static readonly TimeSpan AotSetupTimeout = TimeSpan.FromMinutes(10);

	private readonly LocalNuGetFeed _feed = new();
	private readonly List<string> _tempDirectories = [];
	private readonly IMessageSink _diagnosticMessageSink;
	private string? _extractionDirectory;

	public NuGetAutoInstrFixture(IMessageSink diagnosticMessageSink) => _diagnosticMessageSink = diagnosticMessageSink;

	/// <summary>Path to the extracted redistributable (profiler infrastructure).</summary>
	public string InstallationDirectory { get; private set; } = string.Empty;

	/// <summary>Path to the published NuGetAutoInstr.Net10.dll (profiler-based).</summary>
	public string Net10AppPath { get; private set; } = string.Empty;

	/// <summary>Path to the published NuGetAutoInstr.Aot.Net10 native exe.</summary>
	public string AotAppPath { get; private set; } = string.Empty;

	/// <summary>The packed package version.</summary>
	public string PackageVersion { get; private set; } = string.Empty;

	/// <summary>Whether the profiler-based fixture initialized successfully.</summary>
	public bool IsReady { get; private set; }

	/// <summary>Whether the AOT fixture initialized successfully.</summary>
	public bool AotIsReady { get; private set; }

	/// <summary>Error message if initialization failed.</summary>
	public string? InitializationError { get; private set; }

	/// <summary>Error message if AOT initialization failed.</summary>
	public string? AotInitializationError { get; private set; }

	public async Task InitializeAsync()
	{
		// Shared setup budget covers: extract zip + pack + restore + publish (normal app).
		// AOT setup runs under its own timeout so it cannot inherit an already-expired token.
		using var sharedSetupCts = new CancellationTokenSource(SharedSetupTimeout);
		var ct = sharedSetupCts.Token;
		var solutionRoot = FindSolutionRoot();
		string? configFilePath = null;

		try
		{
			const string redistributeHelp = "Run './build.sh redistribute' to build the redistributable zips first.";
			// 1. Find and extract the redistributable zip (for profiler infrastructure)
			var zipPath = FindDistributionZip(solutionRoot)
				?? throw new FileNotFoundException(
					"Redistributable zip not found. " +
					redistributeHelp + " " +
					"Then rerun the integration tests.\n" +
					$"Expected: elastic-dotnet-instrumentation-{GetPlatformZipSuffix()}.zip " +
					$"under {Path.Combine(solutionRoot, ".artifacts", "elastic-distribution")}");

			_extractionDirectory = Path.Combine(
				Path.GetTempPath(), $"edot-nuget-autoinstr-redist-{Guid.NewGuid():N}");
			_tempDirectories.Add(_extractionDirectory);
			WriteFixtureLog($"Extracting redistributable zip '{zipPath}' to '{_extractionDirectory}'.");
			ZipFile.ExtractToDirectory(zipPath, _extractionDirectory);
			InstallationDirectory = _extractionDirectory;

			// 2. Pack the AutoInstrumentation NuGet
			var autoInstrProjectPath = Path.Combine(solutionRoot,
				"src", "Elastic.OpenTelemetry.AutoInstrumentation",
				"Elastic.OpenTelemetry.AutoInstrumentation.csproj");
			WriteFixtureLog($"Packing NuGet package from '{autoInstrProjectPath}'.");
			await _feed.PackProjectAsync(autoInstrProjectPath, ct, _diagnosticMessageSink).ConfigureAwait(false);
			WriteFixtureLog("Pack step completed.");

			PackageVersion = _feed.GetPackageVersion("Elastic.OpenTelemetry.AutoInstrumentation")
				?? throw new InvalidOperationException(
					"Failed to determine package version after packing. " +
					$"Feed directory: {_feed.FeedPath}");

			// 3. Write nuget.config for the consumer apps
			var nugetConfigDir = Path.Combine(Path.GetTempPath(), $"edot-nuget-autoinstr-config-{Guid.NewGuid():N}");
			_tempDirectories.Add(nugetConfigDir);
			_feed.WriteNuGetConfig(nugetConfigDir);
			configFilePath = Path.Combine(nugetConfigDir, "nuget.config");

			// 4. Publish the net10.0 consumer app
			Net10AppPath = await PublishConsumerAppAsync(
				solutionRoot, "NuGetAutoInstr.Net10", "NuGetAutoInstr.Net10.dll",
				configFilePath, ct).ConfigureAwait(false);
			IsReady = true;
		}
		catch (Exception ex)
		{
			InitializationError = ex.ToString();
			WriteFixtureLog($"Initialization failed: {InitializationError}");
		}

		// 5. Publish AOT consumer app (separate try/catch — independent of profiler tests)
		if (string.IsNullOrEmpty(PackageVersion) || configFilePath is null)
		{
			AotInitializationError = $"Shared setup failed — cannot build AOT app.\n{InitializationError}";
			return;
		}

		try
		{
			using var aotSetupCts = new CancellationTokenSource(AotSetupTimeout);
			var aotProjectPath = Path.Combine(solutionRoot,
				"test-applications", "NuGetAutoInstr.Aot.Net10", "NuGetAutoInstr.Aot.Net10.csproj");

			// AOT is the scenario that most visibly surfaced the stale-project.assets.json issue:
			// ilc reads the full transitive graph at publish time, so a cache-hit restore that
			// inherited ProjectReference-era deps from a prior solution build would feed ilc an
			// older Google.Protobuf (with AOT analysis warnings) instead of the centrally-pinned
			// version. Isolate obj/bin per consumer app to keep .artifacts/ out of the picture.
			var (aotBaseOutputArg, aotBaseIntermediateOutputArg, _) =
				NuGetPackageFixture.CreateIsolatedBuildPaths("NuGetAutoInstr.Aot.Net10", _tempDirectories);

			await RunDotnetAsync(
				$"restore \"{aotProjectPath}\" " +
				$"--configfile \"{configFilePath}\" " +
				$"{aotBaseOutputArg} {aotBaseIntermediateOutputArg} " +
				$"-p:ElasticOtelVersion={PackageVersion}", aotSetupCts.Token).ConfigureAwait(false);

			var aotPublishDir = Path.Combine(Path.GetTempPath(),
				$"edot-nuget-autoinstr-aot-{Guid.NewGuid():N}");
			_tempDirectories.Add(aotPublishDir);

			// AOT publish — produces a native executable, takes longer than normal publish
			await RunDotnetAsync(
				$"publish \"{aotProjectPath}\" --no-restore " +
				$"-c Release -o \"{aotPublishDir}\" " +
				$"{aotBaseOutputArg} {aotBaseIntermediateOutputArg} " +
				$"-p:ElasticOtelVersion={PackageVersion}", aotSetupCts.Token).ConfigureAwait(false);

			// AOT produces a native exe (no .dll)
			var aotExeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
				? "NuGetAutoInstr.Aot.Net10.exe"
				: "NuGetAutoInstr.Aot.Net10";

			AotAppPath = Path.Combine(aotPublishDir, aotExeName);

			if (!File.Exists(AotAppPath))
				throw new FileNotFoundException(
					$"AOT published app not found at expected path: {AotAppPath}");

			AotIsReady = true;
		}
		catch (Exception ex)
		{
			AotInitializationError = ex.ToString();
			WriteFixtureLog($"AOT initialization failed: {AotInitializationError}");
		}
	}

	private async Task<string> PublishConsumerAppAsync(
		string solutionRoot, string projectName, string outputFileName,
		string configFilePath, CancellationToken ct)
	{
		var projectPath = Path.Combine(solutionRoot,
			"test-applications", projectName, $"{projectName}.csproj");

		// Isolate obj/bin from .artifacts/ so a prior solution-wide build's project.assets.json
		// can't cache-hit this restore and override the PackageReference path we're exercising.
		// See NuGetPackageFixture.CreateIsolatedBuildPaths for the detailed rationale.
		var (baseOutputArg, baseIntermediateOutputArg, _) =
			NuGetPackageFixture.CreateIsolatedBuildPaths(projectName, _tempDirectories);

		await RunDotnetAsync(
			$"restore \"{projectPath}\" " +
			$"--configfile \"{configFilePath}\" " +
			$"{baseOutputArg} {baseIntermediateOutputArg} " +
			$"-p:ElasticOtelVersion={PackageVersion}", ct).ConfigureAwait(false);

		var publishDir = Path.Combine(Path.GetTempPath(),
			$"edot-{projectName.ToLowerInvariant()}-{Guid.NewGuid():N}");
		_tempDirectories.Add(publishDir);

		WriteFixtureLog($"Publishing {projectName}");
		WriteFixtureLog($"consumerProjectPath: {projectPath}");
		WriteFixtureLog($"configFilePath: {configFilePath}");
		WriteFixtureLog($"PackageVersion: {PackageVersion}");
		WriteFixtureLog($"Working directory: {Environment.CurrentDirectory}");
		WriteFixtureLog($"NUGET_PACKAGES: {Environment.GetEnvironmentVariable("NUGET_PACKAGES") ?? "<null>"}");

		await RunDotnetAsync(
			$"publish \"{projectPath}\" --no-restore " +
			$"-c Release -o \"{publishDir}\" " +
			$"{baseOutputArg} {baseIntermediateOutputArg} " +
			$"-p:ElasticOtelVersion={PackageVersion}", ct).ConfigureAwait(false);

		var appPath = Path.Combine(publishDir, outputFileName);

		if (!File.Exists(appPath))
			throw new FileNotFoundException(
				$"Published app not found at expected path: {appPath}");

		return appPath;
	}

	public Task DisposeAsync()
	{
		_feed.Dispose();

		foreach (var dir in _tempDirectories)
		{
			if (Directory.Exists(dir))
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

		var arch = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
		return $"linux-glibc-{arch}";
	}

	private void WriteFixtureLog(string message) =>
		_diagnosticMessageSink.OnMessage(
			new DiagnosticMessage($"[{DateTimeOffset.UtcNow:O}] [NuGetAutoInstrFixture] {message}"));

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

[CollectionDefinition("NuGetAutoInstrumentation")]
public class NuGetAutoInstrCollection : ICollectionFixture<NuGetAutoInstrFixture>;
