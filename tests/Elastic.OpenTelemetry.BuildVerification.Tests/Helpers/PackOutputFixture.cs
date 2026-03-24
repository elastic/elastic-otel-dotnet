// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.OpenTelemetry.BuildVerification.Tests.Helpers;

public class PackOutputFixture : IAsyncLifetime, IDisposable
{
	public string PackOutputDir { get; } = Path.Combine(
		Path.GetTempPath(), $"edot-pack-test-{Guid.NewGuid():N}"[..30]);

	public async Task InitializeAsync()
	{
		Directory.CreateDirectory(PackOutputDir);

		var packElastic = await DotNetHelper.PackAsync(
			"src/Elastic.OpenTelemetry/Elastic.OpenTelemetry.csproj",
			PackOutputDir);
		Assert.True(packElastic.ExitCode == 0,
			$"Failed to pack Elastic.OpenTelemetry:\n{packElastic.Error}\n{packElastic.Output}");

		var packAutoInst = await DotNetHelper.PackAsync(
			"src/Elastic.OpenTelemetry.AutoInstrumentation/Elastic.OpenTelemetry.AutoInstrumentation.csproj",
			PackOutputDir);
		Assert.True(packAutoInst.ExitCode == 0,
			$"Failed to pack AutoInstrumentation:\n{packAutoInst.Error}\n{packAutoInst.Output}");
	}

	public Task DisposeAsync()
	{
		Dispose();
		return Task.CompletedTask;
	}

	public void Dispose()
	{
		if (Directory.Exists(PackOutputDir))
			Directory.Delete(PackOutputDir, true);
	}
}
