// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections;
using System.Text;
using Elastic.OpenTelemetry.Configuration;
using Microsoft.Extensions.Configuration;
using static Elastic.OpenTelemetry.Configuration.ElasticDefaults;

namespace Elastic.OpenTelemetry.Tests.Configuration;

public class EnabledDefaultsConfigurationTest
{

	[Theory]
	[ClassData(typeof(DefaultsData))]
	public void ParsesFromConfiguration(string optionValue, Action<ElasticDefaults> asserts)
	{
		var json = $$"""
					 {
					 	"Elastic": {
					 		"OpenTelemetry": {
					 			"EnabledDefaults": "{{optionValue}}",
					 		}
					 	}
					 }
					 """;

		var config = new ConfigurationBuilder()
			.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
			.Build();
		var sut = new ElasticOpenTelemetryOptions(config, new Hashtable());
		asserts(sut.EnabledDefaults);
	}

	[Theory]
	[ClassData(typeof(DefaultsData))]
	internal void ParseFromEnvironment(string optionValue, Action<ElasticDefaults> asserts)
	{

		var env = new Hashtable { { EnvironmentVariables.ELASTIC_OTEL_ENABLE_ELASTIC_DEFAULTS, optionValue } };
		var sut = new ElasticOpenTelemetryOptions(env);

		asserts(sut.EnabledDefaults);
	}

	internal class DefaultsData : TheoryData<string, Action<ElasticDefaults>>
	{
		public DefaultsData()
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
