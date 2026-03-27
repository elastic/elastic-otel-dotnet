// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using System.Text;

namespace Elastic.OpenTelemetry.IntegrationTests.Helpers;

/// <summary>
/// Launches a child process (typically a test application) with configured environment variables,
/// captures its output, and locates the EDOT log file produced during the run.
/// </summary>
/// <remarks>
/// <para>Automatically injects EDOT file-logging env vars unless the caller provides them:</para>
/// <list type="bullet">
///   <item><c>ELASTIC_OTEL_LOG_TARGETS=file</c></item>
///   <item><c>OTEL_DOTNET_AUTO_LOG_DIRECTORY={unique temp dir}</c></item>
///   <item><c>OTEL_LOG_LEVEL=trace</c></item>
///   <item><c>ELASTIC_OTEL_SKIP_OTLP_EXPORTER=true</c></item>
/// </list>
/// </remarks>
internal sealed class TestAppRunner : IAsyncDisposable
{
	private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

	private readonly string _appPath;
	private readonly Dictionary<string, string> _environmentVariables;
	private readonly string _logDirectory;
	private readonly bool _ownsLogDirectory;
	private readonly object _stdoutLock = new();
	private readonly object _stderrLock = new();
	private Process? _process;
	private readonly StringBuilder _stdout = new();
	private readonly StringBuilder _stderr = new();

	/// <param name="appPath">
	/// Path to the application DLL (launched via <c>dotnet {appPath}</c>)
	/// or a native executable.
	/// </param>
	/// <param name="environmentVariables">
	/// Environment variables for the child process. EDOT log defaults are merged
	/// underneath — caller-provided values take precedence.
	/// </param>
	public TestAppRunner(string appPath, Dictionary<string, string> environmentVariables)
	{
		_appPath = appPath;

		// Use caller's log directory if specified, otherwise create a temp one
		if (environmentVariables.TryGetValue("OTEL_DOTNET_AUTO_LOG_DIRECTORY", out var logDir))
		{
			_logDirectory = logDir;
			_ownsLogDirectory = false;
		}
		else
		{
			_logDirectory = Path.Combine(Path.GetTempPath(), $"edot-test-{Guid.NewGuid():N}");
			Directory.CreateDirectory(_logDirectory);
			_ownsLogDirectory = true;
		}

		// EDOT log defaults — caller overrides take precedence
		_environmentVariables = new Dictionary<string, string>
		{
			["ELASTIC_OTEL_LOG_TARGETS"] = "file",
			["OTEL_DOTNET_AUTO_LOG_DIRECTORY"] = _logDirectory,
			["OTEL_LOG_LEVEL"] = "trace",
			["ELASTIC_OTEL_SKIP_OTLP_EXPORTER"] = "true",
		};

		foreach (var (key, value) in environmentVariables)
			_environmentVariables[key] = value;
	}

	/// <summary>Path to the EDOT log file produced by the app, or <c>null</c> if not found.</summary>
	public string? EdotLogFilePath { get; private set; }

	/// <summary>Process exit code, available after <see cref="WaitForExitAsync"/>.</summary>
	public int? ExitCode { get; private set; }

	/// <summary>Captured stdout from the child process.</summary>
	public string StandardOutput { get { lock (_stdoutLock) return _stdout.ToString(); } }

	/// <summary>Captured stderr from the child process.</summary>
	public string StandardError { get { lock (_stderrLock) return _stderr.ToString(); } }

	/// <summary>
	/// Builds a diagnostic dump of the process output for use in assertion messages.
	/// Includes stdout, stderr, and the tail of the EDOT log file if available.
	/// </summary>
	/// <remarks>
	/// Without this, CI failures like <c>Assert.Equal(0, runner.ExitCode)</c> only show
	/// "Expected: 0, Actual: 1" with no process output — making it impossible to
	/// diagnose what went wrong without reproducing locally.
	/// </remarks>
	public string GetDiagnosticSummary()
	{
		var sb = new StringBuilder();
		sb.AppendLine($"App: {_appPath}");
		sb.AppendLine($"Exit code: {ExitCode}");

		var stdout = StandardOutput;
		if (!string.IsNullOrWhiteSpace(stdout))
			sb.AppendLine($"stdout:\n{stdout}");

		var stderr = StandardError;
		if (!string.IsNullOrWhiteSpace(stderr))
			sb.AppendLine($"stderr:\n{stderr}");

		if (EdotLogFilePath is not null && File.Exists(EdotLogFilePath))
		{
			var logLines = File.ReadAllLines(EdotLogFilePath);
			var tail = logLines.Length > 50 ? logLines[^50..] : logLines;
			sb.AppendLine($"EDOT log ({logLines.Length} lines, last {tail.Length}):\n{string.Join("\n", tail)}");
		}

		return sb.ToString();
	}

	/// <summary>
	/// Asserts that the process exited with code 0. On failure, includes full
	/// diagnostic output (stdout, stderr, EDOT log tail) in the assertion message.
	/// </summary>
	public void AssertExitCodeZero() =>
		Assert.True(ExitCode == 0,
			$"Process exited with code {ExitCode}.\n{GetDiagnosticSummary()}");

	/// <summary>Starts the child process.</summary>
	public void Start()
	{
		// .dll → run via dotnet CLI; anything else → run directly as a native executable.
		// This handles both .exe (Windows / net462) and extensionless native AOT binaries
		// on Linux. The previous check (.exe only) caused AOT apps on Linux to be
		// incorrectly launched via "dotnet <path>", which fails because the native ELF
		// binary is not a .NET assembly.
		var isDll = _appPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
		var psi = new ProcessStartInfo
		{
			FileName = isDll ? "dotnet" : _appPath,
			Arguments = isDll ? $"\"{_appPath}\"" : "",
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
		};

		foreach (var (key, value) in _environmentVariables)
			psi.EnvironmentVariables[key] = value;

		// Wire handlers before Start() so no output is missed between
		// process creation and BeginOutputReadLine().
		_process = new Process { StartInfo = psi };
		_process.OutputDataReceived += (_, e) =>
		{
			if (e.Data is not null)
				lock (_stdoutLock)
					_stdout.AppendLine(e.Data);
		};
		_process.ErrorDataReceived += (_, e) =>
		{
			if (e.Data is not null)
				lock (_stderrLock)
					_stderr.AppendLine(e.Data);
		};

		if (!_process.Start())
			throw new InvalidOperationException($"Failed to start process: {psi.FileName} {psi.Arguments}");

		_process.BeginOutputReadLine();
		_process.BeginErrorReadLine();
	}

	/// <summary>
	/// Waits for the child process to exit. Kills the process tree if the timeout expires.
	/// </summary>
	public async Task WaitForExitAsync(TimeSpan? timeout = null)
	{
		if (_process is null)
			throw new InvalidOperationException("Process has not been started. Call Start() first.");

		timeout ??= DefaultTimeout;

		using var cts = new CancellationTokenSource(timeout.Value);

		try
		{
			await _process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			_process.Kill(entireProcessTree: true);
			throw new TimeoutException(
				$"Process did not exit within {timeout.Value.TotalSeconds}s.\n" +
				$"stdout:\n{StandardOutput}\nstderr:\n{StandardError}");
		}

		ExitCode = _process.ExitCode;
		DiscoverLogFile();
	}

	/// <summary>
	/// Convenience method: starts the process and waits for it to exit.
	/// Most test apps self-terminate after initialization, making this the common case.
	/// </summary>
	public async Task RunToCompletionAsync(TimeSpan? timeout = null)
	{
		Start();
		await WaitForExitAsync(timeout).ConfigureAwait(false);
	}

	/// <summary>Kills the process if it hasn't exited.</summary>
	public async Task StopAsync(TimeSpan? timeout = null)
	{
		if (_process is null || _process.HasExited)
			return;

		timeout ??= TimeSpan.FromSeconds(5);
		_process.Kill(entireProcessTree: true);

		using var cts = new CancellationTokenSource(timeout.Value);

		try
		{
			await _process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// Process refused to die — nothing more we can do
			return;
		}

		ExitCode = _process.ExitCode;
		DiscoverLogFile();
	}

	public async ValueTask DisposeAsync()
	{
		if (_process is not null && !_process.HasExited)
			await StopAsync().ConfigureAwait(false);

		_process?.Dispose();

		if (_ownsLogDirectory && Directory.Exists(_logDirectory))
		{
			try
			{ Directory.Delete(_logDirectory, true); }
			catch
			{
				// Best-effort cleanup
			}
		}
	}

	private void DiscoverLogFile()
	{
		if (!Directory.Exists(_logDirectory))
			return;

		// Pick the newest log file — avoids stale files from prior crashed runs
		EdotLogFilePath = Directory.GetFiles(_logDirectory, "edot-*.log")
			.OrderByDescending(File.GetLastWriteTimeUtc)
			.FirstOrDefault();
	}
}
