// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Elastic.OpenTelemetry.IntegrationTests.Helpers;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Elastic.OpenTelemetry.IntegrationTests;

/// <summary>
/// xUnit collection fixture that packs the Elastic.OpenTelemetry NuGet package,
/// creates a local NuGet feed, and builds the NuGetConsumer test apps.
/// Runs once per <c>[Collection("NuGetPackage")]</c> test collection.
/// </summary>
/// <remarks>
///
/// <para>Builds the following consumer apps:</para>
/// <list type="bullet">
///   <item><b>NuGetConsumer.Net8</b> — net8.0 hosting/DI path (AddElasticOpenTelemetry)</item>
///   <item><b>NuGetConsumer.Net462</b> — net462 manual builder path
///     (Sdk.CreateTracerProviderBuilder().WithElasticDefaults()) — represents realistic legacy
///     .NET Framework usage without any hosting dependency</item>
/// </list>
/// <para>The net462 app only builds and runs on Windows.</para>
/// </remarks>
public class NuGetPackageFixture : IAsyncLifetime
{
	internal static readonly TimeSpan PostTimeoutLogDrain = TimeSpan.FromSeconds(30);
	private static readonly TimeSpan SharedSetupTimeout = TimeSpan.FromMinutes(20);
	private static readonly TimeSpan Net462SetupTimeout = TimeSpan.FromMinutes(20);

	private readonly Stopwatch _sw = Stopwatch.StartNew();
	private readonly LocalNuGetFeed _feed = new();
	private readonly List<string> _publishDirectories = [];
	private readonly IMessageSink _diagnosticMessageSink;
	private string? _nugetConfigDirectory;

	public NuGetPackageFixture(IMessageSink diagnosticMessageSink) => _diagnosticMessageSink = diagnosticMessageSink;

	/// <summary>Path to the published NuGetConsumer.Net8.dll, ready to run via <c>dotnet</c>.</summary>
	public string Net8AppPath { get; private set; } = string.Empty;

	/// <summary>
	/// Path to the published NuGetConsumer.Net462.exe, ready to run directly. Windows only.
	/// Uses the manual builder path (Sdk.CreateTracerProviderBuilder().WithElasticDefaults()) —
	/// no hosting dependency — representing realistic legacy .NET Framework usage.
	/// </summary>
	public string Net462AppPath { get; private set; } = string.Empty;

	/// <summary>The version of the packed Elastic.OpenTelemetry NuGet package.</summary>
	public string PackageVersion { get; private set; } = string.Empty;

	/// <summary>Whether the net8.0 fixture initialized successfully.</summary>
	public bool IsReady { get; private set; }

	/// <summary>Whether the net462 fixture initialized successfully.</summary>
	public bool Net462IsReady { get; private set; }

	/// <summary>Error message if initialization failed.</summary>
	public string? InitializationError { get; private set; }

	/// <summary>Error message if net462 initialization failed.</summary>
	public string? Net462InitializationError { get; private set; }

	public async Task InitializeAsync()
	{
		// Shared setup budget covers: pack + restore + publish for net8.
		// net462 setup runs under a separate timeout to avoid inheriting an expired token.
		using var sharedSetupCts = new CancellationTokenSource(SharedSetupTimeout);
		var ct = sharedSetupCts.Token;
		var solutionRoot = FindSolutionRoot();
		string? configFilePath = null;

		WriteFixtureLog($"Starting fixture initialization. Timeout={SharedSetupTimeout.TotalMinutes:F0}m. OS={RuntimeInformation.OSDescription}. SolutionRoot='{solutionRoot}'.");

		try
		{
			var projectPath = Path.Combine(solutionRoot, "src", "Elastic.OpenTelemetry", "Elastic.OpenTelemetry.csproj");

			WriteFixtureLog($"[Stage 1/4] Packing NuGet package from '{projectPath}'.");
			await _feed.PackProjectAsync(projectPath, ct, _diagnosticMessageSink).ConfigureAwait(false);

			PackageVersion = _feed.GetPackageVersion("Elastic.OpenTelemetry")
				?? throw new InvalidOperationException(
					"Failed to determine package version after packing. " +
					$"Feed directory: {_feed.FeedPath}");
			WriteFixtureLog($"[Stage 1/4] Pack completed. PackageVersion='{PackageVersion}'. FeedPath='{_feed.FeedPath}'.");

			// 2. Write a temp nuget.config for the consumer apps' restore
			_nugetConfigDirectory = Path.Combine(Path.GetTempPath(), $"edot-nuget-config-{Guid.NewGuid():N}");
			_feed.WriteNuGetConfig(_nugetConfigDirectory);
			configFilePath = Path.Combine(_nugetConfigDirectory, "nuget.config");
			WriteFixtureLog($"[Stage 2/4] nuget.config written to '{configFilePath}'.");

			// 3. Build net8.0 consumer app
			WriteFixtureLog($"[Stage 3/4] Publishing NuGetConsumer.Net8.");
			Net8AppPath = await PublishConsumerAppAsync(
				solutionRoot, "NuGetConsumer.Net8", "NuGetConsumer.Net8.dll",
				configFilePath, ct).ConfigureAwait(false);
			WriteFixtureLog($"[Stage 3/4] NuGetConsumer.Net8 published to '{Net8AppPath}'.");
			IsReady = true;
			WriteFixtureLog($"Fixture ready (net8). Total elapsed: {_sw.Elapsed.TotalSeconds:F1}s.");
		}
		catch (Exception ex)
		{
			InitializationError = ex.ToString();
			WriteFixtureLog($"Initialization failed at {_sw.Elapsed.TotalSeconds:F1}s: {InitializationError}");
		}

		// 4. Build net462 consumer app (Windows only, requires shared pack step to have succeeded)
		//    Uses the manual builder path (WithElasticDefaults) — no hosting dependency —
		//    representing realistic legacy .NET Framework usage.
		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			Net462InitializationError = ".NET Framework tests require Windows.";
			return;
		}

		if (string.IsNullOrEmpty(PackageVersion) || configFilePath is null)
		{
			Net462InitializationError = $"Shared pack step failed — cannot build net462 app.\n{InitializationError}";
			return;
		}

		try
		{
			WriteFixtureLog($"[Stage 4/4] Publishing NuGetConsumer.Net462. Timeout={Net462SetupTimeout.TotalMinutes:F0}m.");
			using var net462SetupCts = new CancellationTokenSource(Net462SetupTimeout);
			Net462AppPath = await PublishConsumerAppAsync(
				solutionRoot, "NuGetConsumer.Net462", "NuGetConsumer.Net462.exe",
				configFilePath, net462SetupCts.Token).ConfigureAwait(false);
			Net462IsReady = true;
			WriteFixtureLog($"[Stage 4/4] NuGetConsumer.Net462 published to '{Net462AppPath}'. Total elapsed: {_sw.Elapsed.TotalSeconds:F1}s.");
		}
		catch (Exception ex)
		{
			Net462InitializationError = ex.ToString();
			WriteFixtureLog($"[Stage 4/4] net462 initialization failed at {_sw.Elapsed.TotalSeconds:F1}s: {Net462InitializationError}");
		}
	}

	private void WriteFixtureLog(string message) =>
		_diagnosticMessageSink.OnMessage(
			new DiagnosticMessage($"[{DateTimeOffset.UtcNow:O}] [+{_sw.Elapsed.TotalSeconds:F1}s] [NuGetPackageFixture] {message}"));

	private async Task<string> PublishConsumerAppAsync(
		string solutionRoot, string projectName, string outputFileName,
		string configFilePath, CancellationToken ct)
	{
		var consumerProjectPath = Path.Combine(solutionRoot,
			"test-applications", projectName, $"{projectName}.csproj");

		WriteFixtureLog($"  Restoring '{projectName}' (elapsed {_sw.Elapsed.TotalSeconds:F1}s). ConfigFile='{configFilePath}'.");
		await RunDotnetAsync(
			$"restore \"{consumerProjectPath}\" " +
			$"--configfile \"{configFilePath}\" " +
			$"-p:ElasticOtelVersion={PackageVersion}", ct).ConfigureAwait(false);
		WriteFixtureLog($"  Restore complete for '{projectName}' (elapsed {_sw.Elapsed.TotalSeconds:F1}s).");

		var publishDir = Path.Combine(Path.GetTempPath(), $"edot-{projectName.ToLowerInvariant()}-{Guid.NewGuid():N}");
		_publishDirectories.Add(publishDir);

		WriteFixtureLog($"  Publishing '{projectName}' to '{publishDir}' (elapsed {_sw.Elapsed.TotalSeconds:F1}s).");
		await RunDotnetAsync(
			$"publish \"{consumerProjectPath}\" --no-restore " +
			$"-c Release -o \"{publishDir}\" " +
			$"-p:ElasticOtelVersion={PackageVersion}", ct).ConfigureAwait(false);
		WriteFixtureLog($"  Publish complete for '{projectName}' (elapsed {_sw.Elapsed.TotalSeconds:F1}s).");

		var appPath = Path.Combine(publishDir, outputFileName);

		if (!File.Exists(appPath))
			throw new FileNotFoundException(
				$"Published app not found at expected path: {appPath}");

		return appPath;
	}

	/// <summary>
	/// Shared helper used by all integration test fixtures to run dotnet commands
	/// with full diagnostic logging via xUnit's <see cref="IMessageSink"/>.
	/// </summary>
	/// <remarks>
	/// <para>The previous per-fixture <c>RunDotnetAsync</c> methods had two CI-visibility
	/// problems: they were <c>static</c> with no access to the diagnostic sink, and they
	/// passed the cancellation token to <c>ReadToEndAsync</c> — so when the fixture
	/// timeout fired, the reads were cancelled too and stdout/stderr were lost. The only
	/// output that reached CI logs was the bare <c>OperationCanceledException</c>.</para>
	/// <para>This method keeps reading after the main timeout so CI gets useful output,
	/// but only for a bounded grace period. On timeout it kills the process, drains
	/// output for up to 30 more seconds, logs partial output, then rethrows. Every
	/// command logs its start time, elapsed duration, and exit code.</para>
	/// </remarks>
	internal static async Task RunDotnetWithLoggingAsync(
		string arguments, Action<string> log, CancellationToken cancellationToken = default)
	{
		var command = $"dotnet {arguments}";
		if (cancellationToken.IsCancellationRequested)
		{
			log($"Cancellation requested before process start: {command}");
			throw new OperationCanceledException(cancellationToken);
		}

		log($"Running: {command}");
		var sw = Stopwatch.StartNew();

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
		catch (OperationCanceledException)
		{
			// Timeout — kill the process and drain buffered output for a bounded grace period.
			try
			{ process.Kill(entireProcessTree: true); }
			catch { /* best effort */ }

			outputDrainCts.CancelAfter(PostTimeoutLogDrain);
			await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);

			log($"TIMEOUT after {sw.Elapsed.TotalSeconds:F1}s: {command}");
			if (outputDrainCts.IsCancellationRequested)
				log($"Stopped waiting for additional output after {PostTimeoutLogDrain.TotalSeconds:F0}s.");
			if (stdout.Length > 0)
				log($"stdout (partial):\n{stdout}");
			if (stderr.Length > 0)
				log($"stderr (partial):\n{stderr}");

			throw;
		}

		await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);

		log($"Completed in {sw.Elapsed.TotalSeconds:F1}s (exit code {process.ExitCode}): dotnet {arguments.Split(' ')[0]}");

		if (process.ExitCode != 0)
		{
			if (stdout.Length > 0)
				log($"stdout:\n{stdout}");
			if (stderr.Length > 0)
				log($"stderr:\n{stderr}");

			throw new InvalidOperationException(
				$"dotnet {arguments.Split(' ')[0]} failed (exit code {process.ExitCode}).\n" +
				$"stdout:\n{stdout}\nstderr:\n{stderr}");
		}
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

	public Task DisposeAsync()
	{
		_feed.Dispose();

		foreach (var dir in _publishDirectories)
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

		if (_nugetConfigDirectory is not null && Directory.Exists(_nugetConfigDirectory))
		{
			try
			{
				Directory.Delete(_nugetConfigDirectory, true);
			}
			catch
			{
				// Best-effort cleanup
			}
		}

		return Task.CompletedTask;
	}

	private Task RunDotnetAsync(string arguments, CancellationToken cancellationToken = default) =>
		RunDotnetWithLoggingAsync(arguments, WriteFixtureLog, cancellationToken);

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

[CollectionDefinition("NuGetPackage")]
public class NuGetPackageCollection : ICollectionFixture<NuGetPackageFixture>;
