// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Runtime.InteropServices;
using Xunit.Abstractions;

namespace Elastic.OpenTelemetry.Tests.Aot
{
	public class NativeAotCompatibilityTests(ITestOutputHelper output)
	{
		private readonly ITestOutputHelper _output = output;

		[Fact]
		public async Task CanPublishAotApp()
		{
			var workingDir = Environment.CurrentDirectory;
			var indexOfSolutionFolder = workingDir.AsSpan().IndexOf("elastic-otel-dotnet");
			workingDir = workingDir.AsSpan().Slice(0, indexOfSolutionFolder + "elastic-otel-dotnet".Length).ToString();
			workingDir = Path.Combine(workingDir, "examples", "Example.AspNetCore.WebApiAot");

			var rid = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win-x64" : "linux-x64";

			var dotnetPublishProcess = new Process
			{
				StartInfo =
				{
					FileName = "dotnet",
					Arguments = $"publish -c Release -r {rid}",
					WorkingDirectory = workingDir,
					RedirectStandardError = true,
					RedirectStandardOutput = true,
					UseShellExecute = false,
					CreateNoWindow = true
				}
			};

			dotnetPublishProcess.ErrorDataReceived += (sender, args) =>
			{
				if (!string.IsNullOrEmpty(args.Data))
				{
					_output.WriteLine("[ERROR] " + args.Data);
				}
			};

			dotnetPublishProcess.OutputDataReceived += (sender, args) =>
			{
				if (!string.IsNullOrEmpty(args.Data))
				{
					_output.WriteLine(args.Data);
				}
			};

			dotnetPublishProcess.Start();
			dotnetPublishProcess.BeginOutputReadLine();
			dotnetPublishProcess.BeginErrorReadLine();

			await dotnetPublishProcess.WaitForExitAsync();

			Assert.Equal(0, dotnetPublishProcess.ExitCode);
		}
	}
}
