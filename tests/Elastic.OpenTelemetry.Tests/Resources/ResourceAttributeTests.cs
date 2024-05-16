// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Extensions;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using Xunit.Abstractions;

namespace Elastic.OpenTelemetry.Tests.Resources;

public sealed class ResourceAttributeTests : IDisposable
{
	private const string OtelResourceAttributes = "OTEL_RESOURCE_ATTRIBUTES";

	private readonly string? _originalOtelResourceAttributesEnvVar = Environment.GetEnvironmentVariable(OtelResourceAttributes);

	private readonly ElasticOpenTelemetryBuilderOptions _options;

	public ResourceAttributeTests(ITestOutputHelper output) =>
		_options = new ElasticOpenTelemetryBuilderOptions()
		{
			Logger = new TestLogger(output),
			DistroOptions = new ElasticOpenTelemetryOptions() { SkipOtlpExporter = true }
		};

	[Fact]
	public void DefaultServiceResourceAttributesAreAdded()
	{
		var exportedItems = new List<Activity>(0);
		var exporter = new InMemoryExporter<Activity>(exportedItems);

		using var session = new ElasticOpenTelemetryBuilder(_options)
			.WithTracing(tpb => tpb.AddProcessor(new SimpleActivityExportProcessor(exporter)))
			.Build();

		var resource = exporter.ParentProvider.GetResource();

		resource.Attributes.Should().ContainSingle(a => a.Key == "service.name")
			.Subject.Value.Should().NotBeNull().And.BeAssignableTo<string>().Which.Should().StartWith("unknown_service:");

		var instanceId = resource.Attributes.Should().ContainSingle(a => a.Key == "service.instance.id")
			.Subject.Value.Should().NotBeNull().And.BeAssignableTo<string>().Subject;

		Guid.TryParseExact(instanceId, "D", out _).Should().BeTrue(); // The distro should set a GUID for the instance ID if not configured by the user
	}

	[Fact]
	public void UserProvidedConfigureResourceValues_ShouldOverrideDefaults()
	{
		const string serviceName = "Test";
		const string serviceVersion = "1.0.0";
		const string serviceInstanceId = "FromConfigureResource";

		var exportedItems = new List<Activity>(0);
		var exporter = new InMemoryExporter<Activity>(exportedItems);

		using var session = new ElasticOpenTelemetryBuilder(_options)
			.WithTracing(tpb => tpb
				.ConfigureResource(rb => rb.AddService(serviceName, serviceVersion: serviceVersion, serviceInstanceId: serviceInstanceId))
				.AddProcessor(new SimpleActivityExportProcessor(exporter)))
			.Build();

		var resource = exporter.ParentProvider.GetResource();

		resource.Attributes.Should().ContainSingle(a => a.Key == "service.name")
			.Subject.Value.Should().NotBeNull().And.BeAssignableTo<string>().Which.Should().Be(serviceName);

		resource.Attributes.Should().ContainSingle(a => a.Key == "service.version")
			.Subject.Value.Should().NotBeNull().And.BeAssignableTo<string>().Which.Should().Be(serviceVersion);

		resource.Attributes.Should().ContainSingle(a => a.Key == "service.instance.id")
			.Subject.Value.Should().NotBeNull().And.BeAssignableTo<string>().Which.Should().Be(serviceInstanceId);
	}

	[Fact]
	public void UserProvided_ResourceEnvironmentVariable_ShouldOverrideDefaults()
	{
		const string serviceName = "service-from-env-var";
		const string serviceInstanceId = "instance-from-env-var";

		Environment.SetEnvironmentVariable(OtelResourceAttributes, $"service.name={serviceName},service.instance.id={serviceInstanceId}");

		var exportedItems = new List<Activity>(0);
		var exporter = new InMemoryExporter<Activity>(exportedItems);

		using var session = new ElasticOpenTelemetryBuilder(_options)
			.WithTracing(tpb => tpb.AddProcessor(new SimpleActivityExportProcessor(exporter)))
			.Build();

		var resource = exporter.ParentProvider.GetResource();

		resource.Attributes.Should().ContainSingle(a => a.Key == "service.name")
			.Subject.Value.Should().NotBeNull().And.BeAssignableTo<string>().Which.Should().Be(serviceName);

		resource.Attributes.Should().ContainSingle(a => a.Key == "service.instance.id")
			.Subject.Value.Should().NotBeNull().And.BeAssignableTo<string>().Which.Should().Be(serviceInstanceId);

		ResetEnvironmentVariables();
	}

	[Fact]
	public void DefaultHostResourceAttributesAreAdded()
	{
		var exportedItems = new List<Activity>(0);
		var exporter = new InMemoryExporter<Activity>(exportedItems);

		using var session = new ElasticOpenTelemetryBuilder(_options)
			.WithTracing(tpb => tpb.AddProcessor(new SimpleActivityExportProcessor(exporter)))
			.Build();

		var resource = exporter.ParentProvider.GetResource();

		resource.Attributes.Should().ContainSingle(a => a.Key == "host.name")
			.Subject.Value.Should().NotBeNull().And.BeAssignableTo<string>().Which.Should().NotBeEmpty();

		resource.Attributes.Should().ContainSingle(a => a.Key == "host.id")
			.Subject.Value.Should().NotBeNull().And.BeAssignableTo<string>().Which.Should().NotBeEmpty();
	}

	private void ResetEnvironmentVariables() =>
		Environment.SetEnvironmentVariable(OtelResourceAttributes, _originalOtelResourceAttributesEnvVar);

	public void Dispose() => ResetEnvironmentVariables();
}
