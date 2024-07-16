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
		var sut = ElasticDefaults.None;

		sut.HasFlag(ElasticDefaults.Tracing).Should().BeFalse();
		sut.HasFlag(ElasticDefaults.Logging).Should().BeFalse();
		sut.HasFlag(ElasticDefaults.Metrics).Should().BeFalse();
	}

	[Fact]
	public void DefaultCtor_SetsExpectedDefaults_WhenNoEnvironmentVariablesAreConfigured()
	{
		var sut = new ElasticOpenTelemetryOptions(new Hashtable
		{
			{OTEL_DOTNET_AUTO_LOG_DIRECTORY, null},
			{OTEL_LOG_LEVEL, null},
			{ELASTIC_OTEL_ENABLE_ELASTIC_DEFAULTS, null},
			{ELASTIC_OTEL_SKIP_OTLP_EXPORTER, null},
		});

		sut.GlobalLogEnabled.Should().Be(false);
		// these default to null because any other value would enable file logging
		sut.LogDirectory.Should().Be(sut.LogDirectoryDefault);
		sut.LogLevel.Should().Be(LogLevel.Warning);

		sut.Defaults.Should().Be(ElasticDefaults.All);
		sut.Defaults.Should().HaveFlag(ElasticDefaults.Tracing);
		sut.Defaults.Should().HaveFlag(ElasticDefaults.Metrics);
		sut.Defaults.Should().HaveFlag(ElasticDefaults.Logging);
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
			{OTEL_DOTNET_AUTO_LOG_DIRECTORY, fileLogDirectory},
			{OTEL_LOG_LEVEL, fileLogLevel},
			{ELASTIC_OTEL_ENABLE_ELASTIC_DEFAULTS, enabledElasticDefaults},
			{ELASTIC_OTEL_SKIP_OTLP_EXPORTER, "true"},
		});

		sut.LogDirectory.Should().Be(fileLogDirectory);
		sut.LogLevel.Should().Be(ToLogLevel(fileLogLevel));
		sut.Defaults.Should().Be(ElasticDefaults.None);
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
		sut.Defaults.Should().Be(ElasticDefaults.None);
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
		sut.Defaults.Should().Be(ElasticDefaults.None);
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
		sut.Defaults.Should().Be(ElasticDefaults.None);
		sut.SkipOtlpExporter.Should().Be(true);
		sut.LoggingSectionLogLevel.Should().Be(loggingSectionDefaultLogLevel);

		var logger = new TestLogger(output);

		sut.LogConfigSources(logger);

		logger.Messages.Count.Should().Be(4);
		foreach (var message in logger.Messages)
			message.Should().EndWith("from [IConfiguration]");
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
			{OTEL_DOTNET_AUTO_LOG_DIRECTORY, fileLogDirectory},
			{OTEL_LOG_LEVEL, fileLogLevel},
			{ELASTIC_OTEL_ENABLE_ELASTIC_DEFAULTS, enabledElasticDefaults},
			{ELASTIC_OTEL_SKIP_OTLP_EXPORTER, "true"},
		});

		sut.LogDirectory.Should().Be(fileLogDirectory);
		sut.LogLevel.Should().Be(ToLogLevel(fileLogLevel));
		sut.Defaults.Should().Be(ElasticDefaults.None);
		sut.SkipOtlpExporter.Should().Be(true);
	}

	[Fact]
	public void InitializedProperties_TakePrecedenceOver_EnvironmentValues()
	{
		const string fileLogDirectory = "C:\\Property";
		const string fileLogLevel = "Critical";

		var sut = new ElasticOpenTelemetryOptions(new Hashtable
		{
			{OTEL_DOTNET_AUTO_LOG_DIRECTORY, "C:\\Temp"},
			{OTEL_LOG_LEVEL, "Information"},
			{ELASTIC_OTEL_ENABLE_ELASTIC_DEFAULTS, "All"},
			{ELASTIC_OTEL_SKIP_OTLP_EXPORTER, "true"},
		})
		{
			LogDirectory = fileLogDirectory,
			LogLevel = ToLogLevel(fileLogLevel) ?? LogLevel.None,
			SkipOtlpExporter = false,
			Defaults = ElasticDefaults.None
		};

		sut.LogDirectory.Should().Be(fileLogDirectory);
		sut.LogLevel.Should().Be(ToLogLevel(fileLogLevel));
		sut.Defaults.Should().Be(ElasticDefaults.None);
		sut.SkipOtlpExporter.Should().Be(false);

		var logger = new TestLogger(output);

		sut.LogConfigSources(logger);

		logger.Messages.Count.Should().Be(4);
		foreach (var message in logger.Messages)
			message.Should().EndWith("from [Property]");
	}

	[Theory]
	[ClassData(typeof(DefaultsData))]
	internal void ElasticDefaults_ConvertsAsExpected(string optionValue, Action<ElasticDefaults> asserts)
	{

		var env = new Hashtable {{ ELASTIC_OTEL_ENABLE_ELASTIC_DEFAULTS, optionValue} };
		var sut = new ElasticOpenTelemetryOptions(env);

		asserts(sut.Defaults);
	}

	internal class DefaultsData : TheoryData<string, Action<ElasticDefaults>>
	{
		public DefaultsData()
		{
			Add("All", a =>
			{
				a.HasFlag(ElasticDefaults.Tracing).Should().BeTrue();
				a.HasFlag(ElasticDefaults.Metrics).Should().BeTrue();
				a.HasFlag(ElasticDefaults.Logging).Should().BeTrue();
				a.Equals(ElasticDefaults.None).Should().BeFalse();
			});

			Add("all", a =>
			{
				a.HasFlag(ElasticDefaults.Tracing).Should().BeTrue();
				a.HasFlag(ElasticDefaults.Metrics).Should().BeTrue();
				a.HasFlag(ElasticDefaults.Logging).Should().BeTrue();
				a.Equals(ElasticDefaults.None).Should().BeFalse();
			});

			Add("Tracing", a =>
			{
				a.HasFlag(ElasticDefaults.Tracing).Should().BeTrue();
				a.HasFlag(ElasticDefaults.Metrics).Should().BeFalse();
				a.HasFlag(ElasticDefaults.Logging).Should().BeFalse();
				a.Equals(ElasticDefaults.None).Should().BeFalse();
			});

			Add("Metrics", a =>
			{
				a.HasFlag(ElasticDefaults.Tracing).Should().BeFalse();
				a.HasFlag(ElasticDefaults.Metrics).Should().BeTrue();
				a.HasFlag(ElasticDefaults.Logging).Should().BeFalse();
				a.Equals(ElasticDefaults.None).Should().BeFalse();
			});

			Add("Logging", a =>
			{
				a.HasFlag(ElasticDefaults.Tracing).Should().BeFalse();
				a.HasFlag(ElasticDefaults.Metrics).Should().BeFalse();
				a.HasFlag(ElasticDefaults.Logging).Should().BeTrue();
				a.Equals(ElasticDefaults.None).Should().BeFalse();
			});

			Add("Tracing,Logging", a =>
			{
				a.HasFlag(ElasticDefaults.Tracing).Should().BeTrue();
				a.HasFlag(ElasticDefaults.Metrics).Should().BeFalse();
				a.HasFlag(ElasticDefaults.Logging).Should().BeTrue();
				a.Equals(ElasticDefaults.None).Should().BeFalse();
			});
			Add("Tracing;Logging", a =>
			{
				a.HasFlag(ElasticDefaults.Tracing).Should().BeTrue();
				a.HasFlag(ElasticDefaults.Metrics).Should().BeFalse();
				a.HasFlag(ElasticDefaults.Logging).Should().BeTrue();
				a.Equals(ElasticDefaults.None).Should().BeFalse();
			});

			Add("tracing,logging,metrics", a =>
			{
				a.HasFlag(ElasticDefaults.Tracing).Should().BeTrue();
				a.HasFlag(ElasticDefaults.Metrics).Should().BeTrue();
				a.HasFlag(ElasticDefaults.Logging).Should().BeTrue();
				a.Equals(ElasticDefaults.None).Should().BeFalse();
			});

			Add("None", a =>
			{
				a.HasFlag(ElasticDefaults.Tracing).Should().BeFalse();
				a.HasFlag(ElasticDefaults.Metrics).Should().BeFalse();
				a.HasFlag(ElasticDefaults.Logging).Should().BeFalse();
				a.Equals(ElasticDefaults.None).Should().BeTrue();
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
				Defaults = ElasticDefaults.None
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
