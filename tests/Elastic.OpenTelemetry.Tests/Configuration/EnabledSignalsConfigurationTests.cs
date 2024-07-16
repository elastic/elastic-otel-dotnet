// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections;
using System.Text;
using Elastic.OpenTelemetry.Configuration;
using Microsoft.Extensions.Configuration;
using static Elastic.OpenTelemetry.Configuration.ElasticOpenTelemetryOptions.Signals;
using static Elastic.OpenTelemetry.Configuration.EnvironmentVariables;

namespace Elastic.OpenTelemetry.Tests.Configuration;

public class EnabledSignalsConfigurationTest
{

	[Theory]
	[ClassData(typeof(SignalsAsStringInConfigurationData))]
	public void ParsesFromConfiguration(string optionValue, Action<ElasticOpenTelemetryOptions.Signals> asserts)
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
		options.EnabledSignals.Should().HaveFlag(Logging);
		options.EnabledSignals.Should().HaveFlag(Metrics);
		options.EnabledSignals.Should().HaveFlag(Tracing);
		options.EnabledSignals.Should().HaveFlag(All);
	}

	[Fact]
	internal void ExplicitlyDisablingASignalDoesNotDisableOthers()
	{
		var env = new Hashtable { { OTEL_DOTNET_AUTO_LOGS_INSTRUMENTATION_ENABLED, "0" } };
		var options = new ElasticOpenTelemetryOptions(env);
		options.EnabledSignals.Should().NotHaveFlag(Logging);
		options.EnabledSignals.Should().HaveFlag(Metrics);
		options.EnabledSignals.Should().HaveFlag(Tracing);
		options.EnabledSignals.Should().NotHaveFlag(All);
	}
	[Theory]
	[InlineData("1", "1", true, true)]
	[InlineData("0", "1", true, false)]
	[InlineData("0", "0", false, false)]
	[InlineData("1", "0", false, true)]
	internal void RespectsOveralSignalsEnvironmentVar(string instrumentation, string logsInstrumentation, bool logEnabled, bool traceEnabled)
	{
		var env = new Hashtable { {OTEL_DOTNET_AUTO_INSTRUMENTATION_ENABLED, instrumentation}, { OTEL_DOTNET_AUTO_LOGS_INSTRUMENTATION_ENABLED, logsInstrumentation } };
		var options = new ElasticOpenTelemetryOptions(env);
		if (logEnabled)
			options.EnabledSignals.Should().HaveFlag(Logging);
		else
			options.EnabledSignals.Should().NotHaveFlag(Logging);

		if (traceEnabled)
			options.EnabledSignals.Should().HaveFlag(Tracing);
		else
			options.EnabledSignals.Should().NotHaveFlag(Tracing);

		if (instrumentation == "0" && logsInstrumentation == "0")
			options.EnabledSignals.Should().Be(None);
		else
			options.EnabledSignals.Should().NotBe(None);
	}

	private class SignalsAsStringInConfigurationData : TheoryData<string, Action<ElasticOpenTelemetryOptions.Signals>>
	{
		public SignalsAsStringInConfigurationData()
		{
			Add("All", a =>
			{
				a.HasFlag(Tracing).Should().BeTrue();
				a.HasFlag(Metrics).Should().BeTrue();
				a.HasFlag(Logging).Should().BeTrue();
				a.Equals(None).Should().BeFalse();
			});

			Add("all", a =>
			{
				a.HasFlag(Tracing).Should().BeTrue();
				a.HasFlag(Metrics).Should().BeTrue();
				a.HasFlag(Logging).Should().BeTrue();
				a.Equals(None).Should().BeFalse();
			});

			Add("Tracing", a =>
			{
				a.HasFlag(Tracing).Should().BeTrue();
				a.HasFlag(Metrics).Should().BeFalse();
				a.HasFlag(Logging).Should().BeFalse();
				a.Equals(None).Should().BeFalse();
			});

			Add("Metrics", a =>
			{
				a.HasFlag(Tracing).Should().BeFalse();
				a.HasFlag(Metrics).Should().BeTrue();
				a.HasFlag(Logging).Should().BeFalse();
				a.Equals(None).Should().BeFalse();
			});

			Add("Logging", a =>
			{
				a.HasFlag(Tracing).Should().BeFalse();
				a.HasFlag(Metrics).Should().BeFalse();
				a.HasFlag(Logging).Should().BeTrue();
				a.Equals(None).Should().BeFalse();
			});

			Add("Tracing,Logging", a =>
			{
				a.HasFlag(Tracing).Should().BeTrue();
				a.HasFlag(Metrics).Should().BeFalse();
				a.HasFlag(Logging).Should().BeTrue();
				a.Equals(None).Should().BeFalse();
			});
			Add("Tracing;Logging", a =>
			{
				a.HasFlag(Tracing).Should().BeTrue();
				a.HasFlag(Metrics).Should().BeFalse();
				a.HasFlag(Logging).Should().BeTrue();
				a.Equals(None).Should().BeFalse();
			});

			Add("tracing,logging,metrics", a =>
			{
				a.HasFlag(Tracing).Should().BeTrue();
				a.HasFlag(Metrics).Should().BeTrue();
				a.HasFlag(Logging).Should().BeTrue();
				a.Equals(None).Should().BeFalse();
			});

			Add("None", a =>
			{
				a.HasFlag(Tracing).Should().BeFalse();
				a.HasFlag(Metrics).Should().BeFalse();
				a.HasFlag(Logging).Should().BeFalse();
				a.Equals(None).Should().BeTrue();
			});
		}
	};
}
