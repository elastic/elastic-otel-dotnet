// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Text.RegularExpressions;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using Xunit.Abstractions;

namespace Elastic.OpenTelemetry.Tests;

public partial class LoggingTests(ITestOutputHelper output)
{
	private readonly ITestOutputHelper _output = output;

	[GeneratedRegex(@"\[\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z\]\[\d{6}\]\[-*\]\[Information\]\s+Elastic Distribution of OpenTelemetry \(EDOT\) \.NET:.*")]
	private static partial Regex EdotPreamble();

	[GeneratedRegex(@"\[\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z\]\[\d{6}\]\[-*\]\[Debug\]\s+Reusing existing shared components\.\s+")]
	private static partial Regex UsingSharedComponents();

	[Fact]
	public void LoggingIsEnabled_WhenConfiguredViaTracerProviderBuilder()
	{
		var logger = new TestLogger(_output);
		var options = new ElasticOpenTelemetryOptions { AdditionalLogger = logger, SkipOtlpExporter = true };

		using var tracerProvider = Sdk.CreateTracerProviderBuilder()
			.WithElasticDefaults(options)
			.Build();

		Assert.Single(logger.Messages.ToArray(), m => EdotPreamble().IsMatch(m));
	}

	[Fact]
	public void LoggingIsEnabled_WhenConfiguredViaMeterProviderBuilder()
	{
		var logger = new TestLogger(_output);
		var options = new ElasticOpenTelemetryOptions { AdditionalLogger = logger, SkipOtlpExporter = true };

		using var tracerProvider = Sdk.CreateMeterProviderBuilder()
			.WithElasticDefaults(options)
			.Build();

		Assert.Single(logger.Messages.ToArray(), m => EdotPreamble().IsMatch(m));
	}

	[Fact]
	public void LoggingPreamble_IsSkipped_WhenReusingSharedComponents()
	{
		var logger = new TestLogger(_output);
		var options = new ElasticOpenTelemetryOptions { AdditionalLogger = logger, SkipOtlpExporter = true };

		using var tracerProvider = Sdk.CreateTracerProviderBuilder()
			.WithElasticDefaults(options)
			.Build();

		var messages = logger.Messages.ToArray();
		Assert.Single(messages, m => EdotPreamble().IsMatch(m));

		using var meterProvider = Sdk.CreateMeterProviderBuilder()
			.WithElasticDefaults(options)
			.ConfigureResource(rb => rb.AddService("Test", "1.0.0"))
			.AddInMemoryExporter(new List<Metric>())
			.Build();

		// On this builder, because we are reusing the same ElasticOpenTelemetryOptions, shared components will be available,
		// and as such, the pre-amble should not be output a second time.
		messages = logger.Messages.ToArray();
		Assert.Single(messages, m => EdotPreamble().IsMatch(m));
		Assert.Contains(messages, m => UsingSharedComponents().IsMatch(m));
	}
}
