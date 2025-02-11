// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Text.RegularExpressions;
using OpenTelemetry;
using Xunit.Abstractions;
using OpenTelemetry.Metrics;

namespace Elastic.OpenTelemetry.Tests;

public partial class LoggingTests(ITestOutputHelper output)
{
	// This regex pattern matches:
	// - A date in the format YYYY-MM-DD
	// - A time in the format HH:MM:SS.mmm
	// - A five-digit number
	// - Any number of dashes within square brackets
	// - The literal string "[Information]" followed by any number of space characters
	// - The literal string "Elastic Distribution of OpenTelemetry(EDOT) .NET:"
	[GeneratedRegex(@"\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}\]\[\d{5}\]\[-*\]\[Information\]\s+Elastic Distribution of OpenTelemetry \(EDOT\) \.NET:.*")]
	private static partial Regex EdotPreamble();

	// This regex pattern matches:
	// - A date in the format YYYY-MM-DD
	// - A time in the format HH:MM:SS.mmm
	// - A five-digit number
	// - Any number of dashes within square brackets
	// - The literal string "[Debug]" followed by any number of space characters
	// - The literal string "Elastic Distribution of OpenTelemetry(EDOT) .NET:"
	[GeneratedRegex(@"\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}\]\[\d{5}\]\[-*\]\[Debug\]\s+Reusing existing shared components\.\s+")]
	private static partial Regex UsingSharedComponents();

	[Fact]
	public void LoggingIsEnabled_WhenConfiguredViaTracerProviderBuilder()
	{
		var logger = new TestLogger(output);
		var options = new ElasticOpenTelemetryOptions { AdditionalLogger = logger, SkipOtlpExporter = true };

		using var tracerProvider = Sdk.CreateTracerProviderBuilder()
			.UseElasticDefaults(options)
			.ConfigureResource(rb => rb.AddService("Test", "1.0.0"))
			.AddInMemoryExporter([])
			.Build();

		Assert.Single(logger.Messages.ToArray(), m => EdotPreamble().IsMatch(m));
	}

	[Fact]
	public void LoggingIsEnabled_WhenConfiguredViaMeterProviderBuilder()
	{
		var logger = new TestLogger(output);
		var options = new ElasticOpenTelemetryOptions { AdditionalLogger = logger, SkipOtlpExporter = true };

		using var tracerProvider = Sdk.CreateMeterProviderBuilder()
			.UseElasticDefaults(options)
			.ConfigureResource(rb => rb.AddService("Test", "1.0.0"))
			.AddInMemoryExporter(new List<Metric>())
			.Build();

		Assert.Single(logger.Messages.ToArray(), m => EdotPreamble().IsMatch(m));
	}

	[Fact]
	public void LoggingPreamble_IsSkipped_WhenReusingSharedComponents()
	{
		var logger = new TestLogger(output);
		var options = new ElasticOpenTelemetryOptions { AdditionalLogger = logger, SkipOtlpExporter = true };

		using var tracerProvider = Sdk.CreateTracerProviderBuilder()
			.UseElasticDefaults(options)
			.ConfigureResource(rb => rb.AddService("Test", "1.0.0"))
			.AddInMemoryExporter([])
			.Build();

		Assert.Single(logger.Messages, m => EdotPreamble().IsMatch(m));

		using var meterProvider = Sdk.CreateMeterProviderBuilder()
			.UseElasticDefaults(options)
			.ConfigureResource(rb => rb.AddService("Test", "1.0.0"))
			.AddInMemoryExporter(new List<Metric>())
			.Build();

		var messages = logger.Messages.ToArray();

		// On this builder, because we are reusing the same options, shared components will be available,
		// and as such, the pre-amble will not be output a second time.
		Assert.Single(messages, m => EdotPreamble().IsMatch(m));
		Assert.Single(messages, m => UsingSharedComponents().IsMatch(m));
	}
}
