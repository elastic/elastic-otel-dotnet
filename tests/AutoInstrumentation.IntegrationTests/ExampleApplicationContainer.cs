// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Runtime.InteropServices;
using DotNet.Testcontainers;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using Nullean.Xunit.Partitions;
using Nullean.Xunit.Partitions.Sdk;
using Xunit;

[assembly: TestFramework(Partition.TestFramework, Partition.Assembly)]

namespace Elastic.OpenTelemetry.AutoInstrumentation.IntegrationTests;

// ReSharper disable once ClassNeverInstantiated.Global
public class ExampleApplicationContainer : IPartitionLifetime
{
	private readonly IContainer _container;
	private readonly IOutputConsumer _output;
	private readonly IFutureDockerImage _image;

	// docker build -t example.autoinstrumentation:latest -f examples/Example.AutoInstrumentation/Dockerfile . \
	//   && docker run -it --rm -p 5000:8080 --name autoin example.autoinstrumentation:latest

	public ExampleApplicationContainer()
	{
		ConsoleLogger.Instance.DebugLogLevelEnabled = true;
		var directory = CommonDirectoryPath.GetSolutionDirectory();
		_image = new ImageFromDockerfileBuilder()
			.WithDockerfileDirectory(directory, string.Empty)
			.WithDockerfile("examples/Example.AutoInstrumentation/Dockerfile")
			.WithLogger(ConsoleLogger.Instance)
			.WithBuildArgument("TARGETARCH", RuntimeInformation.ProcessArchitecture switch
			{
				Architecture.Arm64 => "arm64",
				Architecture.X64 => "x64",
				Architecture.X86 => "x86",
				_ => "unsupported"
			})
			.Build();

		_output = Consume.RedirectStdoutAndStderrToStream(new MemoryStream(), new MemoryStream());
		_container = new ContainerBuilder()
			.WithImage(_image)
			.WithPortBinding(5000, 8080)
			.WithLogger(ConsoleLogger.Instance)
			.WithOutputConsumer(_output)
			.Build();
	}

	public async Task InitializeAsync()
	{
		await _image.CreateAsync().ConfigureAwait(false);

		await _container.StartAsync().ConfigureAwait(false);
	}

	public async Task DisposeAsync() => await _container.StopAsync().ConfigureAwait(false);

	public string FailureTestOutput()
	{
		_output.Stdout.Seek(0, SeekOrigin.Begin);
		using var streamReader = new StreamReader(_output.Stdout, leaveOpen: true);
		return streamReader.ReadToEnd();
	}

	public int? MaxConcurrency => null;
}
