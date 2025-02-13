// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Configuration.Instrumentations;

namespace Elastic.OpenTelemetry.Tests.Configuration.Instrumentations;

public class MetricInstrumentationsTests
{
	[Fact]
	public void AllTest()
	{
		var instrumentations = new MetricInstrumentations(
		[
			MetricInstrumentation.AspNet,
			MetricInstrumentation.AspNetCore,
			MetricInstrumentation.HttpClient,
			MetricInstrumentation.NetRuntime,
			MetricInstrumentation.NServiceBus,
			MetricInstrumentation.Process
		]);

		Assert.Equal("All", instrumentations.ToString());
	}

	[Fact]
	public void SomeTest()
	{
		var instrumentations = new MetricInstrumentations(
		[
			MetricInstrumentation.HttpClient,
			MetricInstrumentation.NetRuntime,
			MetricInstrumentation.NServiceBus,
			MetricInstrumentation.Process
		]);

		Assert.StartsWith("All Except: AspNet, AspNetCore", instrumentations.ToString());
	}

	[Fact]
	public void NoneTest()
	{
		var instrumentations = new MetricInstrumentations([]);

		Assert.Equal("None", instrumentations.ToString());
	}
}
