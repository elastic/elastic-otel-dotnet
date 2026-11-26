// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using System.Runtime.InteropServices;
using Xunit;
using Xunit.Abstractions;

namespace Elastic.OpenTelemetry.AotCompatibility.Tests
{
	public class NativeAotCompatibilityTests(ITestOutputHelper output)
	{
		private readonly ITestOutputHelper _output = output;

		[Trait("Category", "AotCompatibility")]
		[Fact]
		public async Task CanPublishAotApp()
		{
			var solutionDirectory = new DirectoryInfo(AppContext.BaseDirectory);
			while (solutionDirectory is not null && !File.Exists(Path.Combine(solutionDirectory.FullName, "build.bat")))
				solutionDirectory = solutionDirectory.Parent;

			Assert.NotNull(solutionDirectory);
			var workingDir = Path.Combine(solutionDirectory.FullName, "examples", "Example.AspNetCore.WebApiAot");
			var logDirectory = Path.Combine(solutionDirectory.FullName, ".artifacts", "tests", "aot-compatibility");
			Directory.CreateDirectory(logDirectory);

			var rid = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win-x64" : "linux-x64";

			using var dotnetPublishProcess = new Process
			{
				StartInfo =
				{
					FileName = "dotnet",
					ArgumentList = { "publish", "-c", "Release", "-r", rid, $"-bl:{Path.Combine(logDirectory, "{}.binlog")}" },
					WorkingDirectory = workingDir,
					RedirectStandardError = true,
					RedirectStandardOutput = true,
					UseShellExecute = false,
					CreateNoWindow = true
				}
			};
			dotnetPublishProcess.StartInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";

			using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
			using var outputDrainCts = new CancellationTokenSource();
			var stopwatch = Stopwatch.StartNew();

			dotnetPublishProcess.Start();
			var stdoutTask = PumpOutputAsync(dotnetPublishProcess.StandardOutput, string.Empty, outputDrainCts.Token);
			var stderrTask = PumpOutputAsync(dotnetPublishProcess.StandardError, "[ERROR] ", outputDrainCts.Token);

			try
			{
				await dotnetPublishProcess.WaitForExitAsync(timeoutCts.Token);
			}
			catch (OperationCanceledException ex) when (timeoutCts.IsCancellationRequested)
			{
				try
				{ dotnetPublishProcess.Kill(entireProcessTree: true); }
				catch { /* best effort */ }

				throw new TimeoutException("dotnet publish timed out after 10 minutes.", ex);
			}
			finally
			{
				// WaitForExitAsync must not also wait indefinitely for inherited output pipes.
				outputDrainCts.CancelAfter(TimeSpan.FromSeconds(30));
				await Task.WhenAll(stdoutTask, stderrTask);
				if (outputDrainCts.IsCancellationRequested)
					_output.WriteLine("Output pipes remained open; stopped draining after 30 seconds.");
				_output.WriteLine($"dotnet publish finished after {stopwatch.Elapsed.TotalSeconds:F1}s.");
			}

			Assert.Equal(0, dotnetPublishProcess.ExitCode);
		}

		private async Task PumpOutputAsync(StreamReader reader, string prefix, CancellationToken cancellationToken)
		{
			try
			{
				while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
					_output.WriteLine(prefix + line);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
			}
		}
	}
}
