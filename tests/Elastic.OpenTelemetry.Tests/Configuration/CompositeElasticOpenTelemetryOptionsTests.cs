// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Configuration;

namespace Elastic.OpenTelemetry.Tests.Configuration;

public class CompositeElasticOpenTelemetryOptionsTests
{
	[Fact]
	public void TwoInstancesAreEqual_WhenAllValuesMatch()
	{
		var options1 = new CompositeElasticOpenTelemetryOptions();
		var options2 = new CompositeElasticOpenTelemetryOptions();

		Assert.Equal(options1, options2);
	}

	[Fact]
	public void TwoInstancesAreEqual_WhenTraceInstrumentationValuesMatch()
	{
		var options1 = new CompositeElasticOpenTelemetryOptions(new Dictionary<string, object>()
		{
			{ "OTEL_DOTNET_AUTO_TRACES_INSTRUMENTATION_ENABLED", false },
			{ "OTEL_DOTNET_AUTO_TRACES_ELASTICSEARCH_INSTRUMENTATION_ENABLED", true }
		});

		var options2 = new CompositeElasticOpenTelemetryOptions(new Dictionary<string, object>()
		{
			{ "OTEL_DOTNET_AUTO_TRACES_INSTRUMENTATION_ENABLED", false },
			{ "OTEL_DOTNET_AUTO_TRACES_ELASTICSEARCH_INSTRUMENTATION_ENABLED", true }
		});

		Assert.Equal(options1, options2);
	}

	[Fact]
	public void TwoInstancesAreNotEqual_WhenValuesDoNotMatch()
	{
		var options1 = new CompositeElasticOpenTelemetryOptions(new Dictionary<string, object>()
		{
			{ "OTEL_DOTNET_AUTO_TRACES_INSTRUMENTATION_ENABLED", false },
			{ "OTEL_DOTNET_AUTO_TRACES_ELASTICSEARCH_INSTRUMENTATION_ENABLED", true },
			{ "OTEL_DOTNET_AUTO_TRACES_ASPNETCORE_INSTRUMENTATION_ENABLED", true }
		});

		var options2 = new CompositeElasticOpenTelemetryOptions(new Dictionary<string, object>()
		{
			{ "OTEL_DOTNET_AUTO_TRACES_INSTRUMENTATION_ENABLED", false },
			{ "OTEL_DOTNET_AUTO_TRACES_ELASTICSEARCH_INSTRUMENTATION_ENABLED", true }
		});

		Assert.NotEqual(options1, options2);
	}
}
