// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections;
using System.Text;
using Elastic.OpenTelemetry.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
			{ OTEL_DOTNET_AUTO_LOG_DIRECTORY, null },
			{ OTEL_LOG_LEVEL, null },
			{ ELASTIC_OTEL_ENABLE_ELASTIC_DEFAULTS, null },
			{ ELASTIC_OTEL_SKIP_OTLP_EXPORTER, null },
		});

		sut.GlobalLogEnabled.Should().Be(false);
		// these default to null because any other value would enable file logging
		sut.LogDirectory.Should().Be(sut.LogDirectoryDefault);
		sut.LogLevel.Should().Be(LogLevel.Warning);

		sut.EnabledDefaults.Should().Be(ElasticDefaults.All);
		sut.EnabledDefaults.Should().HaveFlag(ElasticDefaults.Tracing);
		sut.EnabledDefaults.Should().HaveFlag(ElasticDefaults.Metrics);
		sut.EnabledDefaults.Should().HaveFlag(ElasticDefaults.Logging);
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
			{ OTEL_DOTNET_AUTO_LOG_DIRECTORY, fileLogDirectory },
			{ OTEL_LOG_LEVEL, fileLogLevel },
			{ ELASTIC_OTEL_ENABLE_ELASTIC_DEFAULTS, enabledElasticDefaults },
			{ ELASTIC_OTEL_SKIP_OTLP_EXPORTER, "true" },
		});

		sut.LogDirectory.Should().Be(fileLogDirectory);
		sut.LogLevel.Should().Be(ToLogLevel(fileLogLevel));
		sut.EnabledDefaults.Should().Be(ElasticDefaults.None);
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
					 			"EnabledDefaults": "{{enabledElasticDefaults}}",
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
		sut.EnabledDefaults.Should().Be(ElasticDefaults.None);
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
					 			"EnabledDefaults": "{{enabledElasticDefaults}}",
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
		sut.EnabledDefaults.Should().Be(ElasticDefaults.None);
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
					 			"EnabledDefaults": "{{enabledElasticDefaults}}",
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
		sut.EnabledDefaults.Should().Be(ElasticDefaults.None);
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
					 			"EnabledDefaults": "All",
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
			{ OTEL_DOTNET_AUTO_LOG_DIRECTORY, fileLogDirectory },
			{ OTEL_LOG_LEVEL, fileLogLevel },
			{ ELASTIC_OTEL_ENABLE_ELASTIC_DEFAULTS, enabledElasticDefaults },
			{ ELASTIC_OTEL_SKIP_OTLP_EXPORTER, "true" },
		});

		sut.LogDirectory.Should().Be(fileLogDirectory);
		sut.LogLevel.Should().Be(ToLogLevel(fileLogLevel));
		sut.EnabledDefaults.Should().Be(ElasticDefaults.None);
		sut.SkipOtlpExporter.Should().Be(true);
	}

	[Fact]
	public void InitializedProperties_TakePrecedenceOver_EnvironmentValues()
	{
		const string fileLogDirectory = "C:\\Property";
		const string fileLogLevel = "Critical";

		var sut = new ElasticOpenTelemetryOptions(new Hashtable
		{
			{ OTEL_DOTNET_AUTO_LOG_DIRECTORY, "C:\\Temp" },
			{ OTEL_LOG_LEVEL, "Information" },
			{ ELASTIC_OTEL_ENABLE_ELASTIC_DEFAULTS, "All" },
			{ ELASTIC_OTEL_SKIP_OTLP_EXPORTER, "true" },
		})
		{
			LogDirectory = fileLogDirectory,
			LogLevel = ToLogLevel(fileLogLevel) ?? LogLevel.None,
			SkipOtlpExporter = false,
			EnabledDefaults = ElasticDefaults.None
		};

		sut.LogDirectory.Should().Be(fileLogDirectory);
		sut.LogLevel.Should().Be(ToLogLevel(fileLogLevel));
		sut.EnabledDefaults.Should().Be(ElasticDefaults.None);
		sut.SkipOtlpExporter.Should().Be(false);

		var logger = new TestLogger(output);

		sut.LogConfigSources(logger);

		logger.Messages.Count.Should().Be(4);
		foreach (var message in logger.Messages)
			message.Should().EndWith("from [Property]");
	}
}
