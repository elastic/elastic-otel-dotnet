// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections;
using System.Text;
using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Configuration.Instrumentations;
using Elastic.OpenTelemetry.Extensions;
using Microsoft.Extensions.Configuration;
using OpenTelemetry;
using Xunit.Abstractions;
using static Elastic.OpenTelemetry.Configuration.Signals;
using static Elastic.OpenTelemetry.Configuration.EnvironmentVariables;

namespace Elastic.OpenTelemetry.Tests.Configuration;

public class EnabledSignalsConfigurationTest(ITestOutputHelper output)
{

	[Theory]
	[ClassData(typeof(SignalsAsStringInConfigurationData))]
	public void ParsesFromConfiguration(string optionValue, Action<Signals> asserts)
	{
		var json = $$"""
					 {
					 	"Elastic": {
					 		"OpenTelemetry": {
					 			"EnabledSignals": "{{optionValue}}",
					 		}
					 	}
					 }
					 """;

		var config = new ConfigurationBuilder()
			.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
			.Build();
		var sut = new ElasticOpenTelemetryOptions(config, new Hashtable());
		asserts(sut.EnabledSignals);
	}

	[Fact]
	internal void ExplicitlySettingASignalDoesNotDisableOthers()
	{
		var env = new Hashtable { { OTEL_DOTNET_AUTO_LOGS_INSTRUMENTATION_ENABLED, "1" } };
		var options = new ElasticOpenTelemetryOptions(env);
		options.EnabledSignals.Should().HaveFlag(Logs);
		options.EnabledSignals.Should().HaveFlag(Metrics);
		options.EnabledSignals.Should().HaveFlag(Traces);
		options.EnabledSignals.Should().HaveFlag(All);
	}

	[Fact]
	internal void ExplicitlyDisablingASignalDoesNotDisableOthers()
	{
		var env = new Hashtable { { OTEL_DOTNET_AUTO_LOGS_INSTRUMENTATION_ENABLED, "0" } };
		var options = new ElasticOpenTelemetryOptions(env);
		options.EnabledSignals.Should().NotHaveFlag(Logs);
		options.EnabledSignals.Should().HaveFlag(Metrics);
		options.EnabledSignals.Should().HaveFlag(Traces);
		options.EnabledSignals.Should().NotHaveFlag(All);
	}

	[Fact]
	public void OptInFromConfig()
	{
		var json = $$"""
					 {
					 	"Elastic": {
					 		"OpenTelemetry": {
					 			"EnabledSignals": "All",
					 			"Tracing" : "AspNet;ElasticTransport"
					 		}
					 	}
					 }
					 """;

		var config = new ConfigurationBuilder()
			.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
			.Build();
		var options = new ElasticOpenTelemetryOptions(config, new Hashtable());

		options.Tracing.Should().HaveCount(2);
	}
	[Fact]
	public void OptOutFromConfig()
	{
		var json = $$"""
					 {
					 	"Elastic": {
					 		"OpenTelemetry": {
					 			"EnabledSignals": "All",
					 			"Tracing" : "-AspNet;-ElasticTransport"
					 		}
					 	}
					 }
					 """;

		var config = new ConfigurationBuilder()
			.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
			.Build();

		var logger = new TestLogger(output);
		var options = new ElasticOpenTelemetryOptions(config, new Hashtable());
		options.LogConfigSources(logger);

		options.Tracing.Should().HaveCount(TraceInstrumentations.All.Count - 2);

		logger.Messages.Should().ContainMatch("*Configured value for Tracing: 'All Except: AspNet, ElasticTransport'*");
	}


	[Theory]
	[InlineData("1", "1", true, true )]
	[InlineData("0", "1", true, false)]
	[InlineData("0", "0", false, false)]
	[InlineData("1", "0", false, true)]
	internal void RespectsOveralSignalsEnvironmentVar(string instrumentation, string metrics, bool metricsEnabled, bool traceEnabled)
	{
		var env = new Hashtable { {OTEL_DOTNET_AUTO_INSTRUMENTATION_ENABLED, instrumentation}, { OTEL_DOTNET_AUTO_METRICS_INSTRUMENTATION_ENABLED, metrics } };
		var options = new ElasticOpenTelemetryOptions(env);
		if (metricsEnabled)
			options.EnabledSignals.Should().HaveFlag(Metrics);
		else
			options.EnabledSignals.Should().NotHaveFlag(Metrics);

		if (traceEnabled)
			options.EnabledSignals.Should().HaveFlag(Traces);
		else
			options.EnabledSignals.Should().NotHaveFlag(Traces);

		if (instrumentation == "0" && metrics == "0")
			options.EnabledSignals.Should().Be(None);
		else
			options.EnabledSignals.Should().NotBe(None);
	}

	[Theory]
	[InlineData("1", "0", true)]
	[InlineData("0", "1", true)]
	[InlineData("0", "0", false)]
	internal void OptInOverridesDefaults(string instrumentation, string metrics, bool enabledMetrics)
	{
		var env = new Hashtable
		{
			{ OTEL_DOTNET_AUTO_INSTRUMENTATION_ENABLED, instrumentation },
			{ "OTEL_DOTNET_AUTO_METRICS_ASPNET_INSTRUMENTATION_ENABLED", metrics }
		};
		var options = new ElasticOpenTelemetryOptions(env);
		if (metrics == "1")
		{
			options.Metrics.Should().Contain(MetricInstrumentation.AspNet);
			//ensure opt in behavior
			if (instrumentation == "0")
				options.Metrics.Should().HaveCount(1);
			//ensure opt out behaviour
			else
				options.Metrics.Should().HaveCount(MetricInstrumentations.All.Count - 1);

		}
		else
			options.Metrics.Should().NotContain(MetricInstrumentation.AspNet);

		if (enabledMetrics)
			options.EnabledSignals.Should().HaveFlag(Metrics);
		else
			options.EnabledSignals.Should().NotHaveFlag(Metrics);
	}



	private class SignalsAsStringInConfigurationData : TheoryData<string, Action<Signals>>
	{
		public SignalsAsStringInConfigurationData()
		{
			Add("All", a =>
			{
				a.HasFlag(Traces).Should().BeTrue();
				a.HasFlag(Metrics).Should().BeTrue();
				a.HasFlag(Logs).Should().BeTrue();
				a.Equals(None).Should().BeFalse();
			});

			Add("all", a =>
			{
				a.HasFlag(Traces).Should().BeTrue();
				a.HasFlag(Metrics).Should().BeTrue();
				a.HasFlag(Logs).Should().BeTrue();
				a.Equals(None).Should().BeFalse();
			});

			Add("Traces", a =>
			{
				a.HasFlag(Traces).Should().BeTrue();
				a.HasFlag(Metrics).Should().BeFalse();
				a.HasFlag(Logs).Should().BeFalse();
				a.Equals(None).Should().BeFalse();
			});

			Add("Metrics", a =>
			{
				a.HasFlag(Traces).Should().BeFalse();
				a.HasFlag(Metrics).Should().BeTrue();
				a.HasFlag(Logs).Should().BeFalse();
				a.Equals(None).Should().BeFalse();
			});

			Add("Logs", a =>
			{
				a.HasFlag(Traces).Should().BeFalse();
				a.HasFlag(Metrics).Should().BeFalse();
				a.HasFlag(Logs).Should().BeTrue();
				a.Equals(None).Should().BeFalse();
			});

			Add("Traces,Logs", a =>
			{
				a.HasFlag(Traces).Should().BeTrue();
				a.HasFlag(Metrics).Should().BeFalse();
				a.HasFlag(Logs).Should().BeTrue();
				a.Equals(None).Should().BeFalse();
			});
			Add("Traces;Logs", a =>
			{
				a.HasFlag(Traces).Should().BeTrue();
				a.HasFlag(Metrics).Should().BeFalse();
				a.HasFlag(Logs).Should().BeTrue();
				a.Equals(None).Should().BeFalse();
			});

			Add("traces,logs,metrics", a =>
			{
				a.HasFlag(Traces).Should().BeTrue();
				a.HasFlag(Metrics).Should().BeTrue();
				a.HasFlag(Logs).Should().BeTrue();
				a.Equals(None).Should().BeFalse();
			});

			Add("None", a =>
			{
				a.HasFlag(Traces).Should().BeFalse();
				a.HasFlag(Metrics).Should().BeFalse();
				a.HasFlag(Logs).Should().BeFalse();
				a.Equals(None).Should().BeTrue();
			});
		}
	};
}
