// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections;
using System.Text;
using Elastic.OpenTelemetry.Configuration;
using Microsoft.Extensions.Configuration;
using static Elastic.OpenTelemetry.Configuration.Signals;
using static Elastic.OpenTelemetry.Configuration.EnvironmentVariables;

namespace Elastic.OpenTelemetry.Tests.Configuration;

public class EnabledSignalsConfigurationTest
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
	[Theory]
	[InlineData("1", "1", true, true, OTEL_DOTNET_AUTO_METRICS_INSTRUMENTATION_ENABLED)]
	[InlineData("0", "1", true, false, OTEL_DOTNET_AUTO_METRICS_INSTRUMENTATION_ENABLED)]
	[InlineData("0", "0", false, false, OTEL_DOTNET_AUTO_METRICS_INSTRUMENTATION_ENABLED)]
	[InlineData("1", "0", false, true, OTEL_DOTNET_AUTO_METRICS_INSTRUMENTATION_ENABLED)]
	[InlineData("1", "1", true, true, "OTEL_DOTNET_AUTO_METRICS_ASPNET_INSTRUMENTATION_ENABLED")]
	[InlineData("0", "1", true, false, "OTEL_DOTNET_AUTO_METRICS_ASPNET_INSTRUMENTATION_ENABLED")]
	[InlineData("0", "0", false, false, "OTEL_DOTNET_AUTO_METRICS_ASPNET_INSTRUMENTATION_ENABLED")]
	[InlineData("1", "0", false, true, "OTEL_DOTNET_AUTO_METRICS_ASPNET_INSTRUMENTATION_ENABLED")]
	internal void RespectsOveralSignalsEnvironmentVar(string instrumentation, string metrics, bool metricsEnabled, bool traceEnabled, string metricsVar)
	{
		var env = new Hashtable { {OTEL_DOTNET_AUTO_INSTRUMENTATION_ENABLED, instrumentation}, { metricsVar, metrics } };
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
	[InlineData("1", "0", false, true, "OTEL_DOTNET_AUTO_METRICS_ASPNET_INSTRUMENTATION_ENABLED")]
	internal void OptInOverridesDefaults(string instrumentation, string metrics, bool metricsEnabled, bool traceEnabled, string metricsVar)
	{
		var env = new Hashtable { {OTEL_DOTNET_AUTO_INSTRUMENTATION_ENABLED, instrumentation}, { metricsVar, metrics } };
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
