// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections;
using System.Diagnostics.Tracing;
using System.Text;
using Elastic.OpenTelemetry.Configuration;
using Xunit.Abstractions;
using static Elastic.OpenTelemetry.Configuration.EnvironmentVariables;
using static Elastic.OpenTelemetry.Diagnostics.LogLevelHelpers;

namespace Elastic.OpenTelemetry.Tests.Configuration;

public class CompositeElasticOpenTelemetryOptionsTests(ITestOutputHelper output)
{
	private const int ExpectedLogsLength = 7;

	[Fact]
	public void DefaultCtor_SetsExpectedDefaults_WhenNoEnvironmentVariablesAreConfigured()
	{
		var sut = new CompositeElasticOpenTelemetryOptions(new Hashtable
		{
			{ OTEL_DOTNET_AUTO_LOG_DIRECTORY, null },
			{ OTEL_LOG_LEVEL, null },
			{ ELASTIC_OTEL_SKIP_OTLP_EXPORTER, null },
			{ ELASTIC_OTEL_SKIP_ASSEMBLY_SCANNING, null },
		});

		Assert.False(sut.GlobalLogEnabled);

		// these default to null because any other value would enable file logging
		Assert.Equal(sut.LogDirectoryDefault, sut.LogDirectory);
		Assert.Equal(LogLevel.Warning, sut.LogLevel);

		Assert.False(sut.SkipOtlpExporter);
		Assert.False(sut.SkipInstrumentationAssemblyScanning);

		var logger = new TestLogger(output);

		sut.LogConfigSources(logger);

		Assert.Equal(ExpectedLogsLength, logger.Messages.Count);

		foreach (var message in logger.Messages)
			Assert.EndsWith("from [Default]", message);
	}

	[Fact]
	public void DefaultCtor_LoadsConfigurationFromEnvironmentVariables()
	{
		const string fileLogDirectory = "C:\\Temp";
		const string fileLogLevel = "Critical";

		var sut = new CompositeElasticOpenTelemetryOptions(new Hashtable
		{
			{ OTEL_DOTNET_AUTO_LOG_DIRECTORY, fileLogDirectory },
			{ OTEL_LOG_LEVEL, fileLogLevel },
			{ ELASTIC_OTEL_SKIP_OTLP_EXPORTER, true },
			{ ELASTIC_OTEL_SKIP_ASSEMBLY_SCANNING, true },
		});

		Assert.Equal(fileLogDirectory, sut.LogDirectory);
		Assert.Equal(ToLogLevel(fileLogLevel), sut.LogLevel);
		Assert.True(sut.SkipOtlpExporter);
		Assert.True(sut.SkipInstrumentationAssemblyScanning);

		var logger = new TestLogger(output);

		sut.LogConfigSources(logger);

		Assert.Contains(logger.Messages, s => s.EndsWith("from [Environment]", StringComparison.Ordinal));
		Assert.Contains(logger.Messages, s => s.EndsWith("from [Default]", StringComparison.Ordinal));
		Assert.DoesNotContain(logger.Messages, s => s.EndsWith("from [IConfiguration]", StringComparison.Ordinal));
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
								"SkipOtlpExporter": true,
								"SkipInstrumentationAssemblyScanning": true
							}
						}
					}
					""";

		var config = new ConfigurationBuilder()
			.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
			.Build();

		var sut = new CompositeElasticOpenTelemetryOptions(config, new Hashtable());

		Assert.Equal(@"C:\Temp", sut.LogDirectory);
		Assert.Equal(ToLogLevel(fileLogLevel), sut.LogLevel);
		Assert.True(sut.SkipOtlpExporter);
		Assert.True(sut.SkipInstrumentationAssemblyScanning);
		Assert.Equal(EventLevel.Warning, sut.EventLogLevel);
		Assert.Equal(LogLevel.Critical, sut.LogLevel);

		var logger = new TestLogger(output);

		sut.LogConfigSources(logger);

		Assert.Equal(ExpectedLogsLength, logger.Messages.Count);

		Assert.Contains(logger.Messages, s => s.EndsWith("from [IConfiguration]", StringComparison.Ordinal));
		Assert.Contains(logger.Messages, s => s.EndsWith("from [Default]", StringComparison.Ordinal));
		Assert.DoesNotContain(logger.Messages, s => s.EndsWith("from [Environment]", StringComparison.Ordinal));
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

		var sut = new CompositeElasticOpenTelemetryOptions(config, new Hashtable());

		Assert.Equal(@"C:\Temp", sut.LogDirectory);
		Assert.Equal(ToLogLevel(loggingSectionLogLevel), sut.LogLevel);
		Assert.True(sut.SkipOtlpExporter);
		Assert.Equal(EventLevel.Warning, sut.EventLogLevel);
		Assert.Equal(LogLevel.Warning, sut.LogLevel);

		var logger = new TestLogger(output);

		sut.LogConfigSources(logger);

		Assert.Contains(logger.Messages, s => s.EndsWith("from [IConfiguration]", StringComparison.Ordinal));
		Assert.Contains(logger.Messages, s => s.EndsWith("from [Default]", StringComparison.Ordinal));
		Assert.DoesNotContain(logger.Messages, s => s.EndsWith("from [Environment]", StringComparison.Ordinal));
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

		var sut = new CompositeElasticOpenTelemetryOptions(config, new Hashtable());

		Assert.Equal(@"C:\Temp", sut.LogDirectory);
		Assert.Equal(ToLogLevel(loggingSectionDefaultLogLevel), sut.LogLevel);
		Assert.True(sut.SkipOtlpExporter);
		Assert.Equal(EventLevel.Informational, sut.EventLogLevel);

		var logger = new TestLogger(output);

		sut.LogConfigSources(logger);

		Assert.Contains(logger.Messages, s => s.EndsWith("from [IConfiguration]", StringComparison.Ordinal));
		Assert.Contains(logger.Messages, s => s.EndsWith("from [Default]", StringComparison.Ordinal));
		Assert.DoesNotContain(logger.Messages, s => s.EndsWith("from [Environment]", StringComparison.Ordinal));
	}

	[Fact]
	public void EnvironmentVariables_TakePrecedenceOver_ConfigValues()
	{
		const string fileLogDirectory = "C:\\Temp";
		const string fileLogLevel = "Critical";

		var json = $$"""
					{
						"Elastic": {
							"OpenTelemetry": {
								"LogDirectory": "C:\\Json",
								"LogLevel": "Trace",
								"ElasticDefaults": "All",
								"SkipOtlpExporter": false,
								"SkipInstrumentationAssemblyScanning": false
							}
						}
					}
					""";

		var config = new ConfigurationBuilder()
			.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
			.Build();

		var sut = new CompositeElasticOpenTelemetryOptions(config, new Hashtable
		{
			{ OTEL_DOTNET_AUTO_LOG_DIRECTORY, fileLogDirectory },
			{ OTEL_LOG_LEVEL, fileLogLevel },
			{ ELASTIC_OTEL_SKIP_OTLP_EXPORTER, true },
			{ ELASTIC_OTEL_SKIP_ASSEMBLY_SCANNING, true }
		});

		Assert.Equal(fileLogDirectory, sut.LogDirectory);
		Assert.Equal(ToLogLevel(fileLogLevel), sut.LogLevel);
		Assert.True(sut.SkipOtlpExporter);
		Assert.True(sut.SkipInstrumentationAssemblyScanning);
	}

	[Fact]
	public void ExplicitOptions_TakePrecedenceOver_ConfigValues()
	{
		const string fileLogDirectory = "C:\\Temp";

		var options = new ElasticOpenTelemetryOptions
		{
			LogDirectory = fileLogDirectory,
			LogLevel = LogLevel.Critical
		};

		var json = $$"""
					{
						"Elastic": {
							"OpenTelemetry": {
								"LogDirectory": "C:\\Json",
								"LogLevel": "Trace",
								"ElasticDefaults": "All",
								"SkipOtlpExporter": false,
								"SkipInstrumentationAssemblyScanning": true
							}
						}
					}
					""";

		var config = new ConfigurationBuilder()
			.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
			.Build();

		var sut = new CompositeElasticOpenTelemetryOptions(config, options);

		Assert.Equal(fileLogDirectory, sut.LogDirectory);
		Assert.Equal(LogLevel.Critical, sut.LogLevel);
		Assert.False(sut.SkipOtlpExporter);
		Assert.True(sut.SkipInstrumentationAssemblyScanning);
	}

	[Fact]
	public void InitializedProperties_TakePrecedenceOver_EnvironmentValues()
	{
		const string fileLogDirectory = "C:\\Property";
		const string fileLogLevel = "Critical";

		var sut = new CompositeElasticOpenTelemetryOptions(new Hashtable
		{
			{ OTEL_DOTNET_AUTO_LOG_DIRECTORY, "C:\\Temp" },
			{ OTEL_LOG_LEVEL, "Information" },
			{ ELASTIC_OTEL_SKIP_OTLP_EXPORTER, true },
			{ ELASTIC_OTEL_SKIP_ASSEMBLY_SCANNING, true }
		})
		{
			LogDirectory = fileLogDirectory,
			LogLevel = ToLogLevel(fileLogLevel) ?? LogLevel.None,
			SkipOtlpExporter = false,
			SkipInstrumentationAssemblyScanning = false,
		};

		Assert.Equal(fileLogDirectory, sut.LogDirectory);
		Assert.Equal(ToLogLevel(fileLogLevel), sut.LogLevel);
		Assert.False(sut.SkipOtlpExporter);
		Assert.False(sut.SkipInstrumentationAssemblyScanning);

		var logger = new TestLogger(output);

		sut.LogConfigSources(logger);

		Assert.Contains(logger.Messages, s => s.EndsWith("from [Property]", StringComparison.Ordinal));
		Assert.Contains(logger.Messages, s => s.EndsWith("from [Default]", StringComparison.Ordinal));
		Assert.DoesNotContain(logger.Messages, s => s.EndsWith("from [Environment]", StringComparison.Ordinal));
	}

	[Fact]
	public void TwoInstancesAreEqual_WhenAllValuesMatch()
	{
		var options1 = new CompositeElasticOpenTelemetryOptions();
		var options2 = new CompositeElasticOpenTelemetryOptions();

		Assert.Equal(options1, options2);
	}

	[Fact]
	public void TwoInstancesAreEqual_WhenTraceInstrumentationValuesMatch()
	{
		var options1 = new CompositeElasticOpenTelemetryOptions(new Dictionary<string, object>()
		{
			{ "OTEL_DOTNET_AUTO_TRACES_INSTRUMENTATION_ENABLED", false },
			{ "OTEL_DOTNET_AUTO_TRACES_ELASTICSEARCH_INSTRUMENTATION_ENABLED", true }
		});

		var options2 = new CompositeElasticOpenTelemetryOptions(new Dictionary<string, object>()
		{
			{ "OTEL_DOTNET_AUTO_TRACES_INSTRUMENTATION_ENABLED", false },
			{ "OTEL_DOTNET_AUTO_TRACES_ELASTICSEARCH_INSTRUMENTATION_ENABLED", true }
		});

		Assert.Equal(options1, options2);
	}

	[Fact]
	public void TwoInstancesAreNotEqual_WhenValuesDoNotMatch()
	{
		var options1 = new CompositeElasticOpenTelemetryOptions(new Dictionary<string, object>()
		{
			{ "OTEL_DOTNET_AUTO_TRACES_INSTRUMENTATION_ENABLED", false },
			{ "OTEL_DOTNET_AUTO_TRACES_ELASTICSEARCH_INSTRUMENTATION_ENABLED", true },
			{ "OTEL_DOTNET_AUTO_TRACES_ASPNETCORE_INSTRUMENTATION_ENABLED", true }
		});

		var options2 = new CompositeElasticOpenTelemetryOptions(new Dictionary<string, object>()
		{
			{ "OTEL_DOTNET_AUTO_TRACES_INSTRUMENTATION_ENABLED", false },
			{ "OTEL_DOTNET_AUTO_TRACES_ELASTICSEARCH_INSTRUMENTATION_ENABLED", true }
		});

		Assert.NotEqual(options1, options2);
	}
}
