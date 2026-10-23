// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using Xunit.Abstractions;

namespace Elastic.OpenTelemetry.IntegrationTests;

public class OperatorImageSmokeTests(OperatorImageSmokeFixture fixture, ITestOutputHelper output)
	: IClassFixture<OperatorImageSmokeFixture>
{
	internal const string ImagePrefixVariable = "EDOT_OPERATOR_SMOKE_IMAGE_PREFIX";
	private const string InstallationDirectory = "/otel-auto-instrumentation-dotnet";

	[OperatorImageTheory]
	[InlineData("amd64", "glibc", false)]
	[InlineData("amd64", "glibc", true)]
	[InlineData("amd64", "musl", false)]
	[InlineData("amd64", "musl", true)]
	[InlineData("arm64", "glibc", false)]
	[InlineData("arm64", "glibc", true)]
	[InlineData("arm64", "musl", false)]
	[InlineData("arm64", "musl", true)]
	[Trait("Category", "OperatorImageSmoke")]
	public async Task OperatorPayload_LoadsNativeProfilerAndEdotPlugin(string architecture, string libc, bool legacyPath)
	{
		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
		var ct = cts.Token;
		var platform = new Platform($"linux/{architecture}");
		var imagePrefix = Environment.GetEnvironmentVariable(ImagePrefixVariable)
			?? throw new InvalidOperationException($"{ImagePrefixVariable} must identify locally built operator images.");
		var operatorImage = new DockerImage($"{imagePrefix}:{architecture}", platform);
		var nativeArchitecture = architecture == "arm64" ? "arm64" : "x64";
		var nativeDirectory = libc == "musl" ? "linux-musl" : "linux";
		var profilerDirectory = legacyPath ? $"{nativeDirectory}-x64" : nativeDirectory;
		var runtimeTag = $"{(libc == "musl" ? "10.0-alpine" : "10.0-noble")}-{(architecture == "arm64" ? "arm64v8" : "amd64")}";
		var runtimeImage = new DockerImage($"mcr.microsoft.com/dotnet/runtime:{runtimeTag}", platform);

		await using var volume = new VolumeBuilder().Build();
		await volume.CreateAsync(ct).ConfigureAwait(false);

		// Docker volumes start root-owned; simulate a writable Kubernetes emptyDir.
		await using var volumeInitializer = new ContainerBuilder(operatorImage)
			.WithCreateParameterModifier(parameters => parameters.User = "0")
			.WithVolumeMount(volume, InstallationDirectory)
			.WithEntrypoint("sh")
			.WithCommand("-ec", $"chmod 0777 {InstallationDirectory}; echo VOLUME_READY")
			.WithWaitStrategy(WaitForCompletion("VOLUME_READY"))
			.Build();
		await RunToCompletionAsync(volumeInitializer, ct).ConfigureAwait(false);

		await using var copier = new ContainerBuilder(operatorImage)
			.WithVolumeMount(volume, InstallationDirectory)
			.WithEntrypoint("sh")
			.WithCommand("-ec", $"""
				test "$(id -u)" -eq 65532
				cp -r /autoinstrumentation/. {InstallationDirectory}
				echo COPY_COMPLETE
				""")
			.WithWaitStrategy(WaitForCompletion("COPY_COMPLETE"))
			.Build();
		await RunToCompletionAsync(copier, ct).ConfigureAwait(false);

		await using var application = new ContainerBuilder(runtimeImage)
			.WithCreateParameterModifier(parameters => parameters.User = "app")
			.WithVolumeMount(volume, InstallationDirectory, AccessMode.ReadOnly)
			.WithBindMount(fixture.ApplicationDirectory, "/app", AccessMode.ReadOnly)
			.WithWorkingDirectory("/app")
			.WithEnvironment("CORECLR_ENABLE_PROFILING", "1")
			.WithEnvironment("CORECLR_PROFILER", "{918728DD-259F-4A6A-AC2B-B85E1B658318}")
			.WithEnvironment("CORECLR_PROFILER_PATH", $"{InstallationDirectory}/{profilerDirectory}/OpenTelemetry.AutoInstrumentation.Native.so")
			.WithEnvironment("DOTNET_STARTUP_HOOKS", $"{InstallationDirectory}/net/OpenTelemetry.AutoInstrumentation.StartupHook.dll")
			.WithEnvironment("DOTNET_ADDITIONAL_DEPS", $"{InstallationDirectory}/AdditionalDeps")
			.WithEnvironment("DOTNET_SHARED_STORE", $"{InstallationDirectory}/store")
			.WithEnvironment("OTEL_DOTNET_AUTO_HOME", InstallationDirectory)
			.WithEnvironment("OTEL_DOTNET_AUTO_PLUGINS", "Elastic.OpenTelemetry.AutoInstrumentationPlugin, Elastic.OpenTelemetry.AutoInstrumentation")
			.WithEnvironment("OTEL_DOTNET_AUTO_RULE_ENGINE_ENABLED", "true")
			.WithEnvironment("OTEL_DOTNET_AUTO_FAIL_FAST_ENABLED", "true")
			.WithEnvironment("OTEL_DOTNET_AUTO_LOGGER", "console")
			.WithEnvironment("OTEL_LOG_LEVEL", "info")
			.WithEnvironment("ELASTIC_OTEL_LOG_TARGETS", "stdout")
			.WithEnvironment("ELASTIC_OTEL_SKIP_OTLP_EXPORTER", "true")
			.WithEnvironment("OTEL_TRACES_EXPORTER", "none")
			.WithEnvironment("OTEL_METRICS_EXPORTER", "none")
			.WithEnvironment("OTEL_LOGS_EXPORTER", "none")
			.WithEntrypoint("sh")
			.WithCommand("-ec", $$"""
				test "$(uname -m)" = "{{(architecture == "arm64" ? "aarch64" : "x86_64")}}"
				test "$(id -u)" -ne 0
				test -r "$DOTNET_STARTUP_HOOKS"
				test -r "$OTEL_DOTNET_AUTO_HOME/net/Elastic.OpenTelemetry.AutoInstrumentation.dll"
				for rid in linux-x64 linux-musl-x64 linux-{{nativeArchitecture}} linux-musl-{{nativeArchitecture}}; do
				  test -L "$OTEL_DOTNET_AUTO_HOME/$rid"
				  case "$(readlink "$OTEL_DOTNET_AUTO_HOME/$rid")" in
				    linux|linux-musl) ;;
				    *) echo "Unexpected symlink target for $rid" >&2; exit 1 ;;
				  esac
				  test -r "$OTEL_DOTNET_AUTO_HOME/$rid/OpenTelemetry.AutoInstrumentation.Native.so"
				done
				for rid in linux linux-musl; do
				  machine=$(od -An -t u2 -j 18 -N 2 "$OTEL_DOTNET_AUTO_HOME/$rid/OpenTelemetry.AutoInstrumentation.Native.so")
				  test "$machine" -eq {{(architecture == "arm64" ? 183 : 62)}}
				done
				exec dotnet AutoInstr.Console.Net10.dll
				""")
			.WithWaitStrategy(WaitForCompletion("APP_COMPLETE"))
			.Build();

		var logs = await RunToCompletionAsync(application, ct).ConfigureAwait(false);
		Assert.Contains("Profiler attached.", logs);
		Assert.Contains("Elastic Distribution of OpenTelemetry (EDOT) .NET:", logs);
		Assert.Contains("APP_COMPLETE", logs);
	}

	private static IWaitForContainerOS WaitForCompletion(string marker) =>
		Wait.ForUnixContainer().UntilMessageIsLogged(marker,
			strategy => strategy.WithMode(WaitStrategyMode.OneShot).WithTimeout(TimeSpan.FromMinutes(2)));

	private async Task<string> RunToCompletionAsync(IContainer container, CancellationToken ct)
	{
		await container.StartAsync(ct).ConfigureAwait(false);
		var exitCode = await container.GetExitCodeAsync(ct).ConfigureAwait(false);
		var (stdout, stderr) = await container.GetLogsAsync(ct: ct).ConfigureAwait(false);
		var logs = $"{stdout}\n{stderr}";
		output.WriteLine(logs);
		Assert.True(exitCode == 0, $"Container {container.Image.FullName} exited with code {exitCode}.\n{logs}");
		return logs;
	}
}

public sealed class OperatorImageSmokeFixture(IMessageSink diagnosticMessageSink) : IAsyncLifetime
{
	public string ApplicationDirectory { get; } =
		Path.Combine(Path.GetTempPath(), $"edot-operator-smoke-{Guid.NewGuid():N}");

	public async Task InitializeAsync()
	{
		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
		var root = CommonDirectoryPath.GetSolutionDirectory().DirectoryPath;
		var project = Path.Combine(root, "test-applications", "AutoInstr.Console.Net10", "AutoInstr.Console.Net10.csproj");
		Directory.CreateDirectory(ApplicationDirectory);
		var binlog = Path.Combine(ApplicationDirectory, "publish.binlog");
		await NuGetPackageFixture.RunDotnetWithLoggingAsync(
			$"publish \"{project}\" -c Release -o \"{ApplicationDirectory}\" /p:UseAppHost=false \"/bl:{binlog}\"",
			message => diagnosticMessageSink.OnMessage(new Xunit.Sdk.DiagnosticMessage(message)),
			cts.Token).ConfigureAwait(false);
	}

	public Task DisposeAsync()
	{
		if (Directory.Exists(ApplicationDirectory))
			Directory.Delete(ApplicationDirectory, true);
		return Task.CompletedTask;
	}
}

public sealed class OperatorImageTheoryAttribute : TheoryAttribute
{
	public OperatorImageTheoryAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(OperatorImageSmokeTests.ImagePrefixVariable)))
			Skip = $"Set {OperatorImageSmokeTests.ImagePrefixVariable} to run the locally built amd64/arm64 operator images. See examples/k8s/README.md.";
	}
}
