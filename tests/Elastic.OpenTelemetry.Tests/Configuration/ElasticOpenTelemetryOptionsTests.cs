// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections;
using System.Diagnostics.Tracing;
using System.Text;
using Elastic.OpenTelemetry.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

using static Elastic.OpenTelemetry.Configuration.EnvironmentVariables;
using static Elastic.OpenTelemetry.Diagnostics.Logging.LogLevelHelpers;

namespace Elastic.OpenTelemetry.Tests.Configuration;

public sealed class ElasticOpenTelemetryOptionsTests(ITestOutputHelper output)
{
	private const int ExpectedLogsLength = 8;

	[Fact]
	public void EnabledElasticDefaults_NoneIncludesExpectedValues()
	{
		var sut = ElasticDefaults.None;

		sut.HasFlag(ElasticDefaults.Traces).Should().BeFalse();
		sut.HasFlag(ElasticDefaults.Logs).Should().BeFalse();
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

		sut.ElasticDefaults.Should().Be(ElasticDefaults.All);
		sut.ElasticDefaults.Should().HaveFlag(ElasticDefaults.Traces);
		sut.ElasticDefaults.Should().HaveFlag(ElasticDefaults.Metrics);
		sut.ElasticDefaults.Should().HaveFlag(ElasticDefaults.Logs);
		sut.SkipOtlpExporter.Should().Be(false);

		var logger = new TestLogger(output);

		sut.LogConfigSources(logger);

		logger.Messages.Count.Should().Be(ExpectedLogsLength);
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
		sut.ElasticDefaults.Should().Be(ElasticDefaults.None);
		sut.SkipOtlpExporter.Should().Be(true);

		var logger = new TestLogger(output);

		sut.LogConfigSources(logger);

		logger.Messages.Should()
			.Contain(s => s.EndsWith("from [Environment]"))
			.And.Contain(s => s.EndsWith("from [Default]"))
			.And.NotContain(s => s.EndsWith("from [IConfiguration]"));


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
					 			"ElasticDefaults": "{{enabledElasticDefaults}}",
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
		sut.ElasticDefaults.Should().Be(ElasticDefaults.None);
		sut.SkipOtlpExporter.Should().Be(true);
		sut.EventLogLevel.Should().Be(EventLevel.Warning);
		sut.LogLevel.Should().Be(LogLevel.Critical);

		var logger = new TestLogger(output);

		sut.LogConfigSources(logger);

		logger.Messages.Count.Should().Be(ExpectedLogsLength);
		logger.Messages.Should()
			.Contain(s => s.EndsWith("from [IConfiguration]"))
			.And.Contain(s => s.EndsWith("from [Default]"))
			.And.NotContain(s => s.EndsWith("from [Environment]"));
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
					 			"ElasticDefaults": "{{enabledElasticDefaults}}",
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
		sut.ElasticDefaults.Should().Be(ElasticDefaults.None);
		sut.SkipOtlpExporter.Should().Be(true);
		sut.LogLevel.Should().Be(LogLevel.Warning);
		sut.EventLogLevel.Should().Be(EventLevel.Warning);

		var logger = new TestLogger(output);

		sut.LogConfigSources(logger);

		logger.Messages.Should()
			.Contain(s => s.EndsWith("from [IConfiguration]"))
			.And.Contain(s => s.EndsWith("from [Default]"))
			.And.NotContain(s => s.EndsWith("from [Environment]"));

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
					 			"ElasticDefaults": "{{enabledElasticDefaults}}",
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
		sut.ElasticDefaults.Should().Be(ElasticDefaults.None);
		sut.SkipOtlpExporter.Should().Be(true);
		sut.EventLogLevel.Should().Be(EventLevel.Informational);

		var logger = new TestLogger(output);

		sut.LogConfigSources(logger);

		logger.Messages.Should()
			.Contain(s => s.EndsWith("from [IConfiguration]"))
			.And.Contain(s => s.EndsWith("from [Default]"))
			.And.NotContain(s => s.EndsWith("from [Environment]"));
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
					 			"ElasticDefaults": "All",
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
		sut.ElasticDefaults.Should().Be(ElasticDefaults.None);
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
			ElasticDefaults = ElasticDefaults.None
		};

		sut.LogDirectory.Should().Be(fileLogDirectory);
		sut.LogLevel.Should().Be(ToLogLevel(fileLogLevel));
		sut.ElasticDefaults.Should().Be(ElasticDefaults.None);
		sut.SkipOtlpExporter.Should().Be(false);

		var logger = new TestLogger(output);

		sut.LogConfigSources(logger);

		logger.Messages.Should()
			.Contain(s => s.EndsWith("from [Property]"))
			.And.Contain(s => s.EndsWith("from [Default]"))
			.And.NotContain(s => s.EndsWith("from [Environment]"));
	}
}
