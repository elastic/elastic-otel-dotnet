// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Runtime.InteropServices;
using DotNet.Testcontainers;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;

namespace Elastic.OpenTelemetry.IntegrationTests;

/// <summary>
/// Tests the full Bootstrap → OpAmp → CentralConfiguration path in a Docker container
/// for process isolation (avoids static state leakage from <c>ElasticOpenTelemetry.Bootstrap</c>).
/// Linux-only — skipped on Windows CI where Docker is unavailable.
/// </summary>
public class OpAmpBootstrapDockerTests : IAsyncLifetime
{
	private readonly OpAmpTestServer.OpAmpTestServer _server;
	private IFutureDockerImage? _image;
	private IContainer? _container;
	private IOutputConsumer? _output;

	public OpAmpBootstrapDockerTests() =>
		_server = new OpAmpTestServer.OpAmpTestServer("""{"log_level":"debug"}""");

	public async Task InitializeAsync()
	{
		// Start the OpAmp test server on all interfaces so Docker containers can reach it.
		await _server.StartAsync("0.0.0.0");

		var directory = CommonDirectoryPath.GetSolutionDirectory();
		_image = new ImageFromDockerfileBuilder()
			.WithDockerfileDirectory(directory, string.Empty)
			.WithDockerfile("test-applications/OpAmpBootstrapTestApp/Dockerfile")
			.WithLogger(ConsoleLogger.Instance)
			.Build();

		await _image.CreateAsync();

		_output = Consume.RedirectStdoutAndStderrToStream(new MemoryStream(), new MemoryStream());
		_container = new ContainerBuilder()
			.WithImage(_image)
			.WithLogger(ConsoleLogger.Instance)
			.WithOutputConsumer(_output)
			.WithCreateParameterModifier(p =>
			{
				// Enable host.docker.internal on Linux (Docker 20.10+)
				p.HostConfig.ExtraHosts ??= [];
				p.HostConfig.ExtraHosts.Add("host.docker.internal:host-gateway");
			})
			.WithEnvironment("ELASTIC_OTEL_OPAMP_ENDPOINT", $"http://host.docker.internal:{_server.Port}")
			.WithEnvironment("OTEL_SERVICE_NAME", "bootstrap-docker-test")
			.WithEnvironment("ELASTIC_OTEL_LOG_TARGETS", "stdout")
			.WithEnvironment("OTEL_LOG_LEVEL", "debug")
			.WithEnvironment("ELASTIC_OTEL_SKIP_OTLP_EXPORTER", "true")
			.Build();

		await _container.StartAsync();
	}

	public async Task DisposeAsync()
	{
		if (_container is not null)
		{
			try
			{ await _container.StopAsync(); }
			catch (ContainerNotRunningException) { /* Container already exited */ }
		}
		await _server.DisposeAsync();
	}

	[NotWindowsCiFact]
	public async Task FullBootstrapPath_ReceivesCentralConfig()
	{
		// The container is a short-lived process — poll stdout until the completion
		// marker appears or we time out. This avoids the flakiness of a fixed delay.
		var output = await PollOutputForMarker("BOOTSTRAP_COMPLETE", TimeSpan.FromSeconds(30));

		Assert.Contains("BOOTSTRAP_COMPLETE", output);
		Assert.Contains("Elastic Distribution of OpenTelemetry (EDOT) .NET:", output);
		Assert.Contains("Successfully retrieved initial central configuration", output);
		Assert.True(_server.RequestCount >= 1, "OpAmp test server should have received at least one request.");
	}

	private async Task<string> PollOutputForMarker(string marker, TimeSpan timeout)
	{
		using var cts = new CancellationTokenSource(timeout);
		var output = string.Empty;

		while (!cts.Token.IsCancellationRequested)
		{
			output = ReadStdout();
			if (output.Contains(marker) || output.Contains("BOOTSTRAP_FAILED"))
				return output;

			try
			{ await Task.Delay(500, cts.Token); }
			catch (OperationCanceledException) { break; }
		}

		// Final read after timeout
		return ReadStdout();
	}

	private string ReadStdout()
	{
		_output!.Stdout.Seek(0, SeekOrigin.Begin);
		using var reader = new StreamReader(_output.Stdout, leaveOpen: true);
		return reader.ReadToEnd();
	}
}

public class NotWindowsCiFact : FactAttribute
{
	public NotWindowsCiFact()
	{
		Timeout = 120_000; // 2 minutes for Docker build + run

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")))
			Skip = "Cannot run Docker tests in a virtualized Windows environment";
	}
}
