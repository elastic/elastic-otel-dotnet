// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections;
using System.Text;
using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using Xunit.Abstractions;

using static Elastic.OpenTelemetry.Configuration.ElasticOpenTelemetryOptions;
using static Elastic.OpenTelemetry.Configuration.EnvironmentVariables;
using static Elastic.OpenTelemetry.Diagnostics.Logging.LogLevelHelpers;

namespace Elastic.OpenTelemetry.Tests.Configuration;

public sealed class ElasticOpenTelemetryOptionsTests(ITestOutputHelper output)
{
	[Fact]
	public void EnabledElasticDefaults_NoneIncludesExpectedValues()
	{
		var sut = EnabledElasticDefaults.None;

		sut.HasFlag(EnabledElasticDefaults.Tracing).Should().BeFalse();
		sut.HasFlag(EnabledElasticDefaults.Logging).Should().BeFalse();
		sut.HasFlag(EnabledElasticDefaults.Metrics).Should().BeFalse();
	}

	[Fact]
	public void DefaultCtor_SetsExpectedDefaults_WhenNoEnvironmentVariablesAreConfigured()
	{
		var sut = new ElasticOpenTelemetryOptions(new Hashtable
		{
			{ELASTIC_OTEL_LOG_DIRECTORY, null},
			{ELASTIC_OTEL_LOG_LEVEL, null},
			{ELASTIC_OTEL_ENABLE_ELASTIC_DEFAULTS, null},
			{ELASTIC_OTEL_SKIP_OTLP_EXPORTER, null},
		});

		sut.GlobalLogEnabled.Should().Be(false);
		// these default to null because any other value would enable file logging
		sut.LogDirectory.Should().Be(sut.LogDirectoryDefault);
		sut.LogLevel.Should().Be(LogLevel.Warning);

		sut.EnableElasticDefaults.Should().Be(string.Empty);
		sut.EnabledDefaults.Should().HaveFlag(EnabledElasticDefaults.Tracing);
		sut.EnabledDefaults.Should().HaveFlag(EnabledElasticDefaults.Metrics);
		sut.EnabledDefaults.Should().HaveFlag(EnabledElasticDefaults.Logging);
		sut.SkipOtlpExporter.Should().Be(false);

		var logger = new TestLogger(output);

		sut.LogConfigSources(logger);

		logger.Messages.Count.Should().Be(4);
		foreach (var message in logger.Messages)
			message.Should().EndWith("from [Default]");
	}

	[Fact]
	public void DefaultCtor_LoadsConfigurationFromEnvironmentVariables()
	{
		const string fileLogDirectory = "C:\\Temp";
		const string fileLogLevel = "Critical";
		const string enabledElasticDefaults = "None";

		var sut = new ElasticOpenTelemetryOptions(new Hashtable
		{
			{ELASTIC_OTEL_LOG_DIRECTORY, fileLogDirectory},
			{ELASTIC_OTEL_LOG_LEVEL, fileLogLevel},
			{ELASTIC_OTEL_ENABLE_ELASTIC_DEFAULTS, enabledElasticDefaults},
			{ELASTIC_OTEL_SKIP_OTLP_EXPORTER, "true"},
		});

		sut.LogDirectory.Should().Be(fileLogDirectory);
		sut.LogLevel.Should().Be(ToLogLevel(fileLogLevel));
		sut.EnableElasticDefaults.Should().Be(enabledElasticDefaults);
		sut.EnabledDefaults.Should().Be(EnabledElasticDefaults.None);
		sut.SkipOtlpExporter.Should().Be(true);

		var logger = new TestLogger(output);

		sut.LogConfigSources(logger);

		logger.Messages.Count.Should().Be(4);
		foreach (var message in logger.Messages)
			message.Should().EndWith("from [Environment]");

	}

	[Fact]
	public void ConfigurationCtor_LoadsConfigurationFromIConfiguration()
	{
		const string loggingSectionLogLevel = "Warning";
		const string fileLogLevel = "Critical";
		const string enabledElasticDefaults = "None";

		var json = $$"""
			{
				"Logging": {
					"LogLevel": {
						"Default": "Information",
						"Elastic.OpenTelemetry": "{{loggingSectionLogLevel}}"
					}
				},
				"Elastic": {
					"OpenTelemetry": {
						"LogDirectory": "C:\\Temp",
						"LogLevel": "{{fileLogLevel}}",
						"EnabledElasticDefaults": "{{enabledElasticDefaults}}",
						"SkipOtlpExporter": true
					}
				}
			}
			""";

		var config = new ConfigurationBuilder()
			.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
			.Build();

		var sut = new ElasticOpenTelemetryOptions(config, new Hashtable());

		sut.LogDirectory.Should().Be(@"C:\Temp");
		sut.LogLevel.Should().Be(ToLogLevel(fileLogLevel));
		sut.EnableElasticDefaults.Should().Be(enabledElasticDefaults);
		sut.EnabledDefaults.Should().Be(EnabledElasticDefaults.None);
		sut.SkipOtlpExporter.Should().Be(true);
		sut.LoggingSectionLogLevel.Should().Be(loggingSectionLogLevel);

		var logger = new TestLogger(output);

		sut.LogConfigSources(logger);

		logger.Messages.Count.Should().Be(4);
		foreach (var message in logger.Messages)
			message.Should().EndWith("from [IConfiguration]");
	}

	[Fact]
	public void ConfigurationCtor_LoadsConfigurationFromIConfiguration_AndFallsBackToLoggingSection_WhenAvailable()
	{
		const string loggingSectionLogLevel = "Warning";
		const string enabledElasticDefaults = "None";

		var json = $$"""
			{
				"Logging": {
					"LogLevel": {
						"Default": "Information",
						"Elastic.OpenTelemetry": "{{loggingSectionLogLevel}}"
					}
				},
				"Elastic": {
					"OpenTelemetry": {
						"LogDirectory": "C:\\Temp",
						"EnabledElasticDefaults": "{{enabledElasticDefaults}}",
						"SkipOtlpExporter": true
					}
				}
			}
			""";

		var config = new ConfigurationBuilder()
			.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
			.Build();

		var sut = new ElasticOpenTelemetryOptions(config, new Hashtable());

		sut.LogDirectory.Should().Be(@"C:\Temp");
		sut.LogLevel.Should().Be(ToLogLevel(loggingSectionLogLevel));
		sut.EnableElasticDefaults.Should().Be(enabledElasticDefaults);
		sut.EnabledDefaults.Should().Be(EnabledElasticDefaults.None);
		sut.SkipOtlpExporter.Should().Be(true);
		sut.LoggingSectionLogLevel.Should().Be(loggingSectionLogLevel);

		var logger = new TestLogger(output);

		sut.LogConfigSources(logger);

		logger.Messages.Count.Should().Be(4);
		foreach (var message in logger.Messages)
			message.Should().EndWith("from [IConfiguration]");

	}

	[Fact]
	public void ConfigurationCtor_LoadsConfigurationFromIConfiguration_AndFallsBackToLoggingSectionDefault_WhenAvailable()
	{
		const string loggingSectionDefaultLogLevel = "Information";
		const string enabledElasticDefaults = "None";

		var json = $$"""
			{
				"Logging": {
					"LogLevel": {
						"Default": "{{loggingSectionDefaultLogLevel}}"
					}
				},
				"Elastic": {
					"OpenTelemetry": {
						"LogDirectory": "C:\\Temp",
						"EnabledElasticDefaults": "{{enabledElasticDefaults}}",
						"SkipOtlpExporter": true
					}
				}
			}
			""";

		var config = new ConfigurationBuilder()
			.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
			.Build();

		var sut = new ElasticOpenTelemetryOptions(config, new Hashtable());

		sut.LogDirectory.Should().Be(@"C:\Temp");
		sut.LogLevel.Should().Be(ToLogLevel(loggingSectionDefaultLogLevel));
		sut.EnableElasticDefaults.Should().Be(enabledElasticDefaults);
		sut.EnabledDefaults.Should().Be(EnabledElasticDefaults.None);
		sut.SkipOtlpExporter.Should().Be(true);
		sut.LoggingSectionLogLevel.Should().Be(loggingSectionDefaultLogLevel);

		var logger = new TestLogger(output);

		sut.LogConfigSources(logger);

		logger.Messages.Count.Should().Be(4);
		foreach (var message in logger.Messages) message.Should().EndWith("from [IConfiguration]");
	}

	[Fact]
	public void EnvironmentVariables_TakePrecedenceOver_ConfigValues()
	{
		const string fileLogDirectory = "C:\\Temp";
		const string fileLogLevel = "Critical";
		const string enabledElasticDefaults = "None";

		var json = $$"""
			{
				"Elastic": {
					"OpenTelemetry": {
						"LogDirectory": "C:\\Json",
						"LogLevel": "Trace",
						"EnabledElasticDefaults": "All",
						"SkipOtlpExporter": false
					}
				}
			}
			""";

		var config = new ConfigurationBuilder()
			.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
			.Build();

		var sut = new ElasticOpenTelemetryOptions(config, new Hashtable
		{
			{ELASTIC_OTEL_LOG_DIRECTORY, fileLogDirectory},
			{ELASTIC_OTEL_LOG_LEVEL, fileLogLevel},
			{ELASTIC_OTEL_ENABLE_ELASTIC_DEFAULTS, enabledElasticDefaults},
			{ELASTIC_OTEL_SKIP_OTLP_EXPORTER, "true"},
		});

		sut.LogDirectory.Should().Be(fileLogDirectory);
		sut.LogLevel.Should().Be(ToLogLevel(fileLogLevel));
		sut.EnableElasticDefaults.Should().Be(enabledElasticDefaults);
		sut.EnabledDefaults.Should().Be(EnabledElasticDefaults.None);
		sut.SkipOtlpExporter.Should().Be(true);
	}

	[Fact]
	public void InitializedProperties_TakePrecedenceOver_EnvironmentValues()
	{
		const string fileLogDirectory = "C:\\Property";
		const string fileLogLevel = "Critical";
		const string enabledElasticDefaults = "None";

		var sut = new ElasticOpenTelemetryOptions(new Hashtable
		{
			{ELASTIC_OTEL_LOG_DIRECTORY, "C:\\Temp"},
			{ELASTIC_OTEL_LOG_LEVEL, "Information"},
			{ELASTIC_OTEL_ENABLE_ELASTIC_DEFAULTS, "All"},
			{ELASTIC_OTEL_SKIP_OTLP_EXPORTER, "true"},
		})
		{
			LogDirectory = fileLogDirectory,
			LogLevel = ToLogLevel(fileLogLevel) ?? LogLevel.None,
			SkipOtlpExporter = false,
			EnableElasticDefaults = enabledElasticDefaults
		};

		sut.LogDirectory.Should().Be(fileLogDirectory);
		sut.LogLevel.Should().Be(ToLogLevel(fileLogLevel));
		sut.EnableElasticDefaults.Should().Be(enabledElasticDefaults);
		sut.EnabledDefaults.Should().Be(EnabledElasticDefaults.None);
		sut.SkipOtlpExporter.Should().Be(false);

		var logger = new TestLogger(output);

		sut.LogConfigSources(logger);

		logger.Messages.Count.Should().Be(4);
		foreach (var message in logger.Messages)
			message.Should().EndWith("from [Property]");
	}

	[Theory]
	[ClassData(typeof(DefaultsData))]
	internal void ElasticDefaults_ConvertsAsExpected(string optionValue, Action<EnabledElasticDefaults> asserts)
	{
		var sut = new ElasticOpenTelemetryOptions
		{
			EnableElasticDefaults = optionValue
		};

		asserts(sut.EnabledDefaults);
	}

	internal class DefaultsData : TheoryData<string, Action<EnabledElasticDefaults>>
	{
		public DefaultsData()
		{
			Add("All", a =>
			{
				a.HasFlag(EnabledElasticDefaults.Tracing).Should().BeTrue();
				a.HasFlag(EnabledElasticDefaults.Metrics).Should().BeTrue();
				a.HasFlag(EnabledElasticDefaults.Logging).Should().BeTrue();
				a.Equals(EnabledElasticDefaults.None).Should().BeFalse();
			});

			Add("all", a =>
			{
				a.HasFlag(EnabledElasticDefaults.Tracing).Should().BeTrue();
				a.HasFlag(EnabledElasticDefaults.Metrics).Should().BeTrue();
				a.HasFlag(EnabledElasticDefaults.Logging).Should().BeTrue();
				a.Equals(EnabledElasticDefaults.None).Should().BeFalse();
			});

			Add("Tracing", a =>
			{
				a.HasFlag(EnabledElasticDefaults.Tracing).Should().BeTrue();
				a.HasFlag(EnabledElasticDefaults.Metrics).Should().BeFalse();
				a.HasFlag(EnabledElasticDefaults.Logging).Should().BeFalse();
				a.Equals(EnabledElasticDefaults.None).Should().BeFalse();
			});

			Add("Metrics", a =>
			{
				a.HasFlag(EnabledElasticDefaults.Tracing).Should().BeFalse();
				a.HasFlag(EnabledElasticDefaults.Metrics).Should().BeTrue();
				a.HasFlag(EnabledElasticDefaults.Logging).Should().BeFalse();
				a.Equals(EnabledElasticDefaults.None).Should().BeFalse();
			});

			Add("Logging", a =>
			{
				a.HasFlag(EnabledElasticDefaults.Tracing).Should().BeFalse();
				a.HasFlag(EnabledElasticDefaults.Metrics).Should().BeFalse();
				a.HasFlag(EnabledElasticDefaults.Logging).Should().BeTrue();
				a.Equals(EnabledElasticDefaults.None).Should().BeFalse();
			});

			Add("Tracing,Logging", a =>
			{
				a.HasFlag(EnabledElasticDefaults.Tracing).Should().BeTrue();
				a.HasFlag(EnabledElasticDefaults.Metrics).Should().BeFalse();
				a.HasFlag(EnabledElasticDefaults.Logging).Should().BeTrue();
				a.Equals(EnabledElasticDefaults.None).Should().BeFalse();
			});

			Add("tracing,logging,metrics", a =>
			{
				a.HasFlag(EnabledElasticDefaults.Tracing).Should().BeTrue();
				a.HasFlag(EnabledElasticDefaults.Metrics).Should().BeTrue();
				a.HasFlag(EnabledElasticDefaults.Logging).Should().BeTrue();
				a.Equals(EnabledElasticDefaults.None).Should().BeFalse();
			});

			Add("None", a =>
			{
				a.HasFlag(EnabledElasticDefaults.Tracing).Should().BeFalse();
				a.HasFlag(EnabledElasticDefaults.Metrics).Should().BeFalse();
				a.HasFlag(EnabledElasticDefaults.Logging).Should().BeFalse();
				a.Equals(EnabledElasticDefaults.None).Should().BeTrue();
			});
		}
	};

	[Fact]
	public void TransactionId_IsNotAdded_WhenElasticDefaultsDoesNotIncludeTracing()
	{
		var options = new ElasticOpenTelemetryBuilderOptions
		{
			Logger = new TestLogger(output),
			DistroOptions = new ElasticOpenTelemetryOptions()
			{
				SkipOtlpExporter = true,
				EnableElasticDefaults = "None"
			}
		};

		const string activitySourceName = nameof(TransactionId_IsNotAdded_WhenElasticDefaultsDoesNotIncludeTracing);

		var activitySource = new ActivitySource(activitySourceName, "1.0.0");

		var exportedItems = new List<Activity>();

		using var session = new ElasticOpenTelemetryBuilder(options)
			.WithTracing(tpb =>
			{
				tpb
					.ConfigureResource(rb => rb.AddService("Test", "1.0.0"))
					.AddSource(activitySourceName)
					.AddInMemoryExporter(exportedItems);
			})
			.Build();

		using (var activity = activitySource.StartActivity(ActivityKind.Internal))
			activity?.SetStatus(ActivityStatusCode.Ok);

		exportedItems.Should().ContainSingle();

		var exportedActivity = exportedItems[0];

		var transactionId = exportedActivity.GetTagItem(TransactionIdProcessor.TransactionIdTagName);

		transactionId.Should().BeNull();
	}
}
