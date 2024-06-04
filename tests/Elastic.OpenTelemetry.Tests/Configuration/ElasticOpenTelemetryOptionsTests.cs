// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Text;
using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Extensions;
using Microsoft.Extensions.Configuration;
using OpenTelemetry;
using Xunit.Abstractions;

using static Elastic.OpenTelemetry.Configuration.ElasticOpenTelemetryOptions;
using static Elastic.OpenTelemetry.Diagnostics.Logging.LogLevelHelpers;

namespace Elastic.OpenTelemetry.Tests.Configuration;

public sealed class ElasticOpenTelemetryOptionsTests(ITestOutputHelper output) : IDisposable
{
	private readonly ITestOutputHelper _output = output;
	private readonly string? _originalFileLogDirectoryEnvVar = Environment.GetEnvironmentVariable(EnvironmentVariables.ELASTIC_OTEL_LOG_DIRECTORY);
	private readonly string? _originalFileLogLevelEnvVar = Environment.GetEnvironmentVariable(EnvironmentVariables.ELASTIC_OTEL_LOG_LEVEL);
	private readonly string? _originalEnableElasticDefaultsEnvVar = Environment.GetEnvironmentVariable(EnvironmentVariables.ELASTIC_OTEL_ENABLE_ELASTIC_DEFAULTS);
	private readonly string? _originalSkipOtlpExporterEnvVar = Environment.GetEnvironmentVariable(EnvironmentVariables.ELASTIC_OTEL_SKIP_OTLP_EXPORTER);

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
		Environment.SetEnvironmentVariable(EnvironmentVariables.ELASTIC_OTEL_LOG_DIRECTORY, null);
		Environment.SetEnvironmentVariable(EnvironmentVariables.ELASTIC_OTEL_LOG_LEVEL, null);
		Environment.SetEnvironmentVariable(EnvironmentVariables.ELASTIC_OTEL_ENABLE_ELASTIC_DEFAULTS, null);
		Environment.SetEnvironmentVariable(EnvironmentVariables.ELASTIC_OTEL_SKIP_OTLP_EXPORTER, null);

		var sut = new ElasticOpenTelemetryOptions();

		// these default to null because any other value would enable file logging
		sut.FileLogDirectory.Should().Be(null);
		sut.FileLogLevel.Should().Be(null);

		sut.EnableElasticDefaults.Should().Be(string.Empty);
		sut.EnabledDefaults.Should().HaveFlag(EnabledElasticDefaults.Tracing);
		sut.EnabledDefaults.Should().HaveFlag(EnabledElasticDefaults.Metrics);
		sut.EnabledDefaults.Should().HaveFlag(EnabledElasticDefaults.Logging);
		sut.SkipOtlpExporter.Should().Be(false);

		var logger = new TestLogger(_output);

		sut.LogConfigSources(logger);

		logger.Messages.Count.Should().Be(4);
		foreach (var message in logger.Messages)
		{
			message.Should().EndWith("from [Default]");
		}

		ResetEnvironmentVariables();
	}

	[Fact]
	public void DefaultCtor_LoadsConfigurationFromEnvironmentVariables()
	{
		const string fileLogDirectory = "C:\\Temp";
		const string fileLogLevel = "Critical";
		const string enabledElasticDefaults = "None";

		Environment.SetEnvironmentVariable(EnvironmentVariables.ELASTIC_OTEL_LOG_DIRECTORY, fileLogDirectory);
		Environment.SetEnvironmentVariable(EnvironmentVariables.ELASTIC_OTEL_LOG_LEVEL, fileLogLevel);
		Environment.SetEnvironmentVariable(EnvironmentVariables.ELASTIC_OTEL_ENABLE_ELASTIC_DEFAULTS, enabledElasticDefaults);
		Environment.SetEnvironmentVariable(EnvironmentVariables.ELASTIC_OTEL_SKIP_OTLP_EXPORTER, "true");

		var sut = new ElasticOpenTelemetryOptions();

		sut.FileLogDirectory.Should().Be(fileLogDirectory);
		sut.FileLogLevel.Should().Be(ToLogLevel(fileLogLevel));
		sut.EnableElasticDefaults.Should().Be(enabledElasticDefaults);
		sut.EnabledDefaults.Should().Be(EnabledElasticDefaults.None);
		sut.SkipOtlpExporter.Should().Be(true);

		var logger = new TestLogger(_output);

		sut.LogConfigSources(logger);

		logger.Messages.Count.Should().Be(4);
		foreach (var message in logger.Messages)
		{
			message.Should().EndWith("from [Environment]");
		}

		ResetEnvironmentVariables();
	}

	[Fact]
	public void ConfigurationCtor_LoadsConfigurationFromIConfiguration()
	{
		const string loggingSectionLogLevel = "Warning";
		const string fileLogLevel = "Critical";
		const string enabledElasticDefaults = "None";

		// Remove all env vars
		Environment.SetEnvironmentVariable(EnvironmentVariables.ELASTIC_OTEL_LOG_DIRECTORY, null);
		Environment.SetEnvironmentVariable(EnvironmentVariables.ELASTIC_OTEL_LOG_LEVEL, null);
		Environment.SetEnvironmentVariable(EnvironmentVariables.ELASTIC_OTEL_ENABLE_ELASTIC_DEFAULTS, null);
		Environment.SetEnvironmentVariable(EnvironmentVariables.ELASTIC_OTEL_SKIP_OTLP_EXPORTER, null);

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
						"FileLogDirectory": "C:\\Temp",
						"FileLogLevel": "{{fileLogLevel}}",
						"EnabledElasticDefaults": "{{enabledElasticDefaults}}",
						"SkipOtlpExporter": true
					}
				}
			}
			""";

		var config = new ConfigurationBuilder()
			.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
			.Build();

		var sut = new ElasticOpenTelemetryOptions(config);

		sut.FileLogDirectory.Should().Be(@"C:\Temp");
		sut.FileLogLevel.Should().Be(ToLogLevel(fileLogLevel));
		sut.EnableElasticDefaults.Should().Be(enabledElasticDefaults);
		sut.EnabledDefaults.Should().Be(EnabledElasticDefaults.None);
		sut.SkipOtlpExporter.Should().Be(true);
		sut.LoggingSectionLogLevel.Should().Be(loggingSectionLogLevel);

		var logger = new TestLogger(_output);

		sut.LogConfigSources(logger);

		logger.Messages.Count.Should().Be(4);
		foreach (var message in logger.Messages)
		{
			message.Should().EndWith("from [IConfiguration]");
		}

		ResetEnvironmentVariables();
	}

	[Fact]
	public void ConfigurationCtor_LoadsConfigurationFromIConfiguration_AndFallsBackToLoggingSection_WhenAvailable()
	{
		const string loggingSectionLogLevel = "Warning";
		const string enabledElasticDefaults = "None";

		// Remove all env vars
		Environment.SetEnvironmentVariable(EnvironmentVariables.ELASTIC_OTEL_LOG_DIRECTORY, null);
		Environment.SetEnvironmentVariable(EnvironmentVariables.ELASTIC_OTEL_LOG_LEVEL, null);
		Environment.SetEnvironmentVariable(EnvironmentVariables.ELASTIC_OTEL_ENABLE_ELASTIC_DEFAULTS, null);
		Environment.SetEnvironmentVariable(EnvironmentVariables.ELASTIC_OTEL_SKIP_OTLP_EXPORTER, null);

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
						"FileLogDirectory": "C:\\Temp",
						"EnabledElasticDefaults": "{{enabledElasticDefaults}}",
						"SkipOtlpExporter": true
					}
				}
			}
			""";

		var config = new ConfigurationBuilder()
			.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
			.Build();

		var sut = new ElasticOpenTelemetryOptions(config);

		sut.FileLogDirectory.Should().Be(@"C:\Temp");
		sut.FileLogLevel.Should().Be(ToLogLevel(loggingSectionLogLevel));
		sut.EnableElasticDefaults.Should().Be(enabledElasticDefaults);
		sut.EnabledDefaults.Should().Be(EnabledElasticDefaults.None);
		sut.SkipOtlpExporter.Should().Be(true);
		sut.LoggingSectionLogLevel.Should().Be(loggingSectionLogLevel);

		var logger = new TestLogger(_output);

		sut.LogConfigSources(logger);

		logger.Messages.Count.Should().Be(4);
		foreach (var message in logger.Messages)
		{
			message.Should().EndWith("from [IConfiguration]");
		}

		ResetEnvironmentVariables();
	}

	[Fact]
	public void ConfigurationCtor_LoadsConfigurationFromIConfiguration_AndFallsBackToLoggingSectionDefault_WhenAvailable()
	{
		const string loggingSectionDefaultLogLevel = "Information";
		const string enabledElasticDefaults = "None";

		// Remove all env vars
		Environment.SetEnvironmentVariable(EnvironmentVariables.ELASTIC_OTEL_LOG_DIRECTORY, null);
		Environment.SetEnvironmentVariable(EnvironmentVariables.ELASTIC_OTEL_LOG_LEVEL, null);
		Environment.SetEnvironmentVariable(EnvironmentVariables.ELASTIC_OTEL_ENABLE_ELASTIC_DEFAULTS, null);
		Environment.SetEnvironmentVariable(EnvironmentVariables.ELASTIC_OTEL_SKIP_OTLP_EXPORTER, null);

		var json = $$"""
			{
				"Logging": {
					"LogLevel": {
						"Default": "{{loggingSectionDefaultLogLevel}}"
					}
				},
				"Elastic": {
					"OpenTelemetry": {
						"FileLogDirectory": "C:\\Temp",
						"EnabledElasticDefaults": "{{enabledElasticDefaults}}",
						"SkipOtlpExporter": true
					}
				}
			}
			""";

		var config = new ConfigurationBuilder()
			.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
			.Build();

		var sut = new ElasticOpenTelemetryOptions(config);

		sut.FileLogDirectory.Should().Be(@"C:\Temp");
		sut.FileLogLevel.Should().Be(ToLogLevel(loggingSectionDefaultLogLevel));
		sut.EnableElasticDefaults.Should().Be(enabledElasticDefaults);
		sut.EnabledDefaults.Should().Be(EnabledElasticDefaults.None);
		sut.SkipOtlpExporter.Should().Be(true);
		sut.LoggingSectionLogLevel.Should().Be(loggingSectionDefaultLogLevel);

		var logger = new TestLogger(_output);

		sut.LogConfigSources(logger);

		logger.Messages.Count.Should().Be(4);
		foreach (var message in logger.Messages)
		{
			message.Should().EndWith("from [IConfiguration]");
		}

		ResetEnvironmentVariables();
	}

	[Fact]
	public void EnvironmentVariables_TakePrecedenceOver_ConfigValues()
	{
		const string fileLogDirectory = "C:\\Temp";
		const string fileLogLevel = "Critical";
		const string enabledElasticDefaults = "None";

		Environment.SetEnvironmentVariable(EnvironmentVariables.ELASTIC_OTEL_LOG_DIRECTORY, fileLogDirectory);
		Environment.SetEnvironmentVariable(EnvironmentVariables.ELASTIC_OTEL_LOG_LEVEL, fileLogLevel);
		Environment.SetEnvironmentVariable(EnvironmentVariables.ELASTIC_OTEL_ENABLE_ELASTIC_DEFAULTS, enabledElasticDefaults);
		Environment.SetEnvironmentVariable(EnvironmentVariables.ELASTIC_OTEL_SKIP_OTLP_EXPORTER, "true");

		var json = $$"""
			{
				"Elastic": {
					"OpenTelemetry": {
						"FileLogDirectory": "C:\\Json",
						"FileLogLevel": "Trace",
						"EnabledElasticDefaults": "All",
						"SkipOtlpExporter": false
					}
				}
			}
			""";

		var config = new ConfigurationBuilder()
			.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
			.Build();

		var sut = new ElasticOpenTelemetryOptions(config);

		sut.FileLogDirectory.Should().Be(fileLogDirectory);
		sut.FileLogLevel.Should().Be(ToLogLevel(fileLogLevel));
		sut.EnableElasticDefaults.Should().Be(enabledElasticDefaults);
		sut.EnabledDefaults.Should().Be(EnabledElasticDefaults.None);
		sut.SkipOtlpExporter.Should().Be(true);

		ResetEnvironmentVariables();
	}

	[Fact]
	public void InitializedProperties_TakePrecedenceOver_EnvironmentValues()
	{
		const string fileLogDirectory = "C:\\Property";
		const string fileLogLevel = "Critical";
		const string enabledElasticDefaults = "None";

		Environment.SetEnvironmentVariable(EnvironmentVariables.ELASTIC_OTEL_LOG_DIRECTORY, "C:\\Temp");
		Environment.SetEnvironmentVariable(EnvironmentVariables.ELASTIC_OTEL_LOG_LEVEL, "Information");
		Environment.SetEnvironmentVariable(EnvironmentVariables.ELASTIC_OTEL_ENABLE_ELASTIC_DEFAULTS, "All");
		Environment.SetEnvironmentVariable(EnvironmentVariables.ELASTIC_OTEL_SKIP_OTLP_EXPORTER, "true");

		var sut = new ElasticOpenTelemetryOptions
		{
			FileLogDirectory = fileLogDirectory,
			FileLogLevel = ToLogLevel(fileLogLevel),
			SkipOtlpExporter = false,
			EnableElasticDefaults = enabledElasticDefaults
		};

		sut.FileLogDirectory.Should().Be(fileLogDirectory);
		sut.FileLogLevel.Should().Be(ToLogLevel(fileLogLevel));
		sut.EnableElasticDefaults.Should().Be(enabledElasticDefaults);
		sut.EnabledDefaults.Should().Be(EnabledElasticDefaults.None);
		sut.SkipOtlpExporter.Should().Be(false);

		var logger = new TestLogger(_output);

		sut.LogConfigSources(logger);

		logger.Messages.Count.Should().Be(4);
		foreach (var message in logger.Messages)
		{
			message.Should().EndWith("from [Property]");
		}

		ResetEnvironmentVariables();
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
			Logger = new TestLogger(_output),
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

	private void ResetEnvironmentVariables()
	{
		Environment.SetEnvironmentVariable(EnvironmentVariables.ELASTIC_OTEL_LOG_DIRECTORY, _originalFileLogDirectoryEnvVar);
		Environment.SetEnvironmentVariable(EnvironmentVariables.ELASTIC_OTEL_LOG_LEVEL, _originalFileLogLevelEnvVar);
		Environment.SetEnvironmentVariable(EnvironmentVariables.ELASTIC_OTEL_ENABLE_ELASTIC_DEFAULTS, _originalEnableElasticDefaultsEnvVar);
		Environment.SetEnvironmentVariable(EnvironmentVariables.ELASTIC_OTEL_SKIP_OTLP_EXPORTER, _originalSkipOtlpExporterEnvVar);
	}

	public void Dispose() => ResetEnvironmentVariables();
}
