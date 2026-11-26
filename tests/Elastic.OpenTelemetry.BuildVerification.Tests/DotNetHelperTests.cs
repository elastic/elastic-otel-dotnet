// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.BuildVerification.Tests.Helpers;

namespace Elastic.OpenTelemetry.BuildVerification.Tests;

public class DotNetHelperTests : IDisposable
{
	private readonly string _directory = Path.Combine(Path.GetTempPath(), $"edot-process-test-{Guid.NewGuid():N}");

	[Theory(Timeout = 60_000)]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Build_StopsOnTimeoutOrCancellation_AndPreservesOutput(bool cancelFromCaller)
	{
		Directory.CreateDirectory(_directory);
		var projectPath = Path.Combine(_directory, "Delay.proj");
		await File.WriteAllTextAsync(projectPath, """
			<Project DefaultTargets="Build">
			  <UsingTask TaskName="Delay" TaskFactory="RoslynCodeTaskFactory"
			             AssemblyFile="$(MSBuildToolsPath)/Microsoft.Build.Tasks.Core.dll">
			    <Task>
			      <Code Type="Fragment" Language="cs">
			        System.Threading.Thread.Sleep(60000);
			      </Code>
			    </Task>
			  </UsingTask>
			  <Target Name="Restore" />
			  <Target Name="Build">
			    <Message Text="BEFORE_DELAY" Importance="high" />
			    <Delay />
			  </Target>
			</Project>
			""");

		var timeout = TimeSpan.FromSeconds(5);
		if (cancelFromCaller)
		{
			using var cts = new CancellationTokenSource(timeout);
			var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
				() => DotNetHelper.BuildAsync(projectPath, cancellationToken: cts.Token));

			Assert.Equal(cts.Token, exception.CancellationToken);
			Assert.Contains("BEFORE_DELAY", exception.Message);
		}
		else
		{
			var result = await DotNetHelper.RunDotNetAsync(
				["msbuild", projectPath, "-nologo"], timeout);

			Assert.Equal(-1, result.ExitCode);
			Assert.Contains("Process timed out", result.Error);
			Assert.Contains("BEFORE_DELAY", result.Output);
		}
	}

	public void Dispose()
	{
		if (Directory.Exists(_directory))
			Directory.Delete(_directory, recursive: true);
	}
}
