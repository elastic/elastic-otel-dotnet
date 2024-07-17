// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections;
using System.Text;
using Elastic.OpenTelemetry.Configuration;
using Microsoft.Extensions.Configuration;
using static Elastic.OpenTelemetry.Configuration.ElasticDefaults;

namespace Elastic.OpenTelemetry.Tests.Configuration;

public class ElasticDefaultsConfigurationTest
{

	[Theory]
	[ClassData(typeof(DefaultsData))]
	public void ParsesFromConfiguration(string optionValue, Action<ElasticDefaults> asserts)
	{
		var json = $$"""
					 {
					 	"Elastic": {
					 		"OpenTelemetry": {
					 			"ElasticDefaults": "{{optionValue}}",
					 		}
					 	}
					 }
					 """;

		var config = new ConfigurationBuilder()
			.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
			.Build();
		var sut = new ElasticOpenTelemetryOptions(config, new Hashtable());
		asserts(sut.ElasticDefaults);
	}

	[Theory]
	[ClassData(typeof(DefaultsData))]
	internal void ParseFromEnvironment(string optionValue, Action<ElasticDefaults> asserts)
	{

		var env = new Hashtable { { EnvironmentVariables.ELASTIC_OTEL_DEFAULTS_ENABLED, optionValue } };
		var sut = new ElasticOpenTelemetryOptions(env);

		asserts(sut.ElasticDefaults);
	}

	internal class DefaultsData : TheoryData<string, Action<ElasticDefaults>>
	{
		public DefaultsData()
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
