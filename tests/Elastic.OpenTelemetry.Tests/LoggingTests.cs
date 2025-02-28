// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Text.RegularExpressions;
using Elastic.OpenTelemetry.Core;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using Xunit.Abstractions;

namespace Elastic.OpenTelemetry.Tests;

// These run in a collection to avoid them running in parallel with other tests that may set the SharedComponents which would cause
// these to fail.
[Collection("Discrete SharedComponents")]
public partial class LoggingTests(ITestOutputHelper output)
{
	[GeneratedRegex(@"\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}\]\[\d{6}\]\[-*\]\[Information\]\s+Elastic Distribution of OpenTelemetry \(EDOT\) \.NET:.*")]
	private static partial Regex EdotPreamble();

	[GeneratedRegex(@"\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}\]\[\d{6}\]\[-*\]\[Debug\]\s+Reusing existing shared components\.\s+")]
	private static partial Regex UsingSharedComponents();

	[Fact]
	public void LoggingIsEnabled_WhenConfiguredViaTracerProviderBuilder()
	{
		ElasticOpenTelemetry.ResetSharedComponentsForTesting();

		var logger = new TestLogger(output);
		var options = new ElasticOpenTelemetryOptions { AdditionalLogger = logger, SkipOtlpExporter = true };

		using var tracerProvider = Sdk.CreateTracerProviderBuilder()
			.WithElasticDefaults(options)
			.Build();

		Assert.Single(logger.Messages.ToArray(), m => EdotPreamble().IsMatch(m));
	}

	[Fact]
	public void LoggingIsEnabled_WhenConfiguredViaMeterProviderBuilder()
	{
		ElasticOpenTelemetry.ResetSharedComponentsForTesting();

		var logger = new TestLogger(output);
		var options = new ElasticOpenTelemetryOptions { AdditionalLogger = logger, SkipOtlpExporter = true };

		using var tracerProvider = Sdk.CreateMeterProviderBuilder()
			.WithElasticDefaults(options)
			.Build();

		Assert.Single(logger.Messages.ToArray(), m => EdotPreamble().IsMatch(m));
	}

	[Fact]
	public void LoggingPreamble_IsSkipped_WhenReusingSharedComponents()
	{
		ElasticOpenTelemetry.ResetSharedComponentsForTesting();

		var logger = new TestLogger(output);
		var options = new ElasticOpenTelemetryOptions { AdditionalLogger = logger, SkipOtlpExporter = true };

		using var tracerProvider = Sdk.CreateTracerProviderBuilder()
			.WithElasticDefaults(options)
			.Build();

		Assert.Single(logger.Messages, m => EdotPreamble().IsMatch(m));

		using var meterProvider = Sdk.CreateMeterProviderBuilder()
			.WithElasticDefaults(options)
			.ConfigureResource(rb => rb.AddService("Test", "1.0.0"))
			.AddInMemoryExporter(new List<Metric>())
			.Build();

		var messages = logger.Messages.ToArray();

		// On this builder, because we are reusing the same ElasticOpenTelemetryOptions, shared components will be available,
		// and as such, the pre-amble should not be output a second time.
		Assert.Single(messages, m => EdotPreamble().IsMatch(m));
		Assert.Contains(messages, m => UsingSharedComponents().IsMatch(m));
	}
}
