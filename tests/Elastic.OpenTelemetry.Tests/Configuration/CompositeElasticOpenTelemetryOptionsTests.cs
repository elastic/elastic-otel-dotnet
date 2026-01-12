// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections;
using System.Text;
using Elastic.OpenTelemetry.Configuration;
using Xunit.Abstractions;

using static Elastic.OpenTelemetry.Configuration.EnvironmentVariables;
using static Elastic.OpenTelemetry.Diagnostics.LogLevelHelpers;

namespace Elastic.OpenTelemetry.Tests.Configuration;

public class CompositeElasticOpenTelemetryOptionsTests(ITestOutputHelper output)
{
	private const int ExpectedLogsLength = 12;

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

	[Fact]
	public void LogValueRedaction_WorksAsExpected()
	{
		var options = new ElasticOpenTelemetryOptions
		{
			OpAmpClientOptions = new OpAmpClientOptions
			{
				Endpoint = "http://my-endpoint.com",
				Headers = "Custom=123,Authorization=ApiKey ABC123,Custom2=ABC"
			}
		};

		var sut = new CompositeElasticOpenTelemetryOptions(options);
		var logger = new TestLogger(output);

		sut.LogConfigSources(logger);

		Assert.Contains(logger.Messages, s => s.EndsWith("Configured value for OpAmpHeaders: 'Custom=123,Authorization=<redacted>,Custom2=ABC' from [Options]", StringComparison.Ordinal));
	}

	[Theory]
	[MemberData(nameof(OpAmpIsEnabledTestData))]
	public void IsOpAmpEnabled_ReturnsExpectedValue(IConfiguration configuration, ElasticOpenTelemetryOptions options, IDictionary dictionary, bool isEnabled)
	{
		var sut = new CompositeElasticOpenTelemetryOptions(configuration, options, dictionary);
		Assert.Equal(isEnabled, sut.IsOpAmpEnabled());
	}

	public static IEnumerable<object[]> OpAmpIsEnabledTestData =>
	[
		[
			new ConfigurationBuilder().Build(),
			new ElasticOpenTelemetryOptions(),
			new Hashtable
			{
				{ ELASTIC_OTEL_OPAMP_ENDPOINT, "http://my-collector-endpoint-from-env-vars.com" }
			},
			false
		],
		[
			new ConfigurationBuilder().Build(),
			new ElasticOpenTelemetryOptions(),
			new Hashtable
			{
				{ ELASTIC_OTEL_OPAMP_ENDPOINT, "http://my-collector-endpoint-from-env-vars.com" },
				{ ELASTIC_OTEL_OPAMP_HEADERS, "Authorization=ApiKey ABC123" },
				{ OTEL_RESOURCE_ATTRIBUTES, "service.name=env-var-service" }
			},
			true
		],
		[
			new ConfigurationBuilder().Build(),
			new ElasticOpenTelemetryOptions(),
			new Hashtable
			{
				{ ELASTIC_OTEL_OPAMP_ENDPOINT, "http://my-collector-endpoint-from-env-vars.com" },
				{ ELASTIC_OTEL_OPAMP_HEADERS, "Authorization=ApiKey ABC123" },
				{ OTEL_RESOURCE_ATTRIBUTES, "service.version=1.0.0" }
			},
			false
		],
		[
			new ConfigurationBuilder().Build(),
			new ElasticOpenTelemetryOptions(),
			new Hashtable
			{
				{ ELASTIC_OTEL_OPAMP_ENDPOINT, "http://my-collector-endpoint-from-env-vars.com" },
				{ ELASTIC_OTEL_OPAMP_HEADERS, "CustomHeader=ABV123" },
				{ OTEL_RESOURCE_ATTRIBUTES, "service.version=1.0.0,service.name=env-var-service" }
			},
			false
		],
		[
			new ConfigurationBuilder().Build(),
			new ElasticOpenTelemetryOptions(),
			new Hashtable
			{
				{ ELASTIC_OTEL_OPAMP_HEADERS, "Authorization=ApiKey ABC123" },
				{ OTEL_RESOURCE_ATTRIBUTES, "service.name=env-var-service" }
			},
			false
		],
		[
			new ConfigurationBuilder().Build(),
			new ElasticOpenTelemetryOptions(),
			new Hashtable
			{
				{ ELASTIC_OTEL_OPAMP_ENDPOINT, "http://my-collector-endpoint-from-env-vars.com" },
				{ OTEL_RESOURCE_ATTRIBUTES, "service.name=env-var-service" }
			},
			false
		],
		[
			new ConfigurationBuilder().Build(),
			new ElasticOpenTelemetryOptions(),
			new Hashtable
			{
				{ ELASTIC_OTEL_OPAMP_ENDPOINT, "http://my-collector-endpoint-from-env-vars.com" },
				{ ELASTIC_OTEL_OPAMP_HEADERS, "Authorization=ApiKey ABC123" }
			},
			false
		],
		[
			new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
			{
				{ "Elastic:OpenTelemetry:OpAmpEndpoint", "http://my-collector-endpoint-from-config.com" },
				{ "Elastic:OpenTelemetry:OpAmpHeaders", "Authorization=ApiKey ABC123" },
				{ "OTEL_RESOURCE_ATTRIBUTES", "service.name=env-var-service" }
			}).Build(),
			new ElasticOpenTelemetryOptions(),
			new Hashtable(),
			true
		],
		[
			new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
			{
				{ "Elastic:OpenTelemetry:OpAmpHeaders", "Authorization=ApiKey ABC123" },
				{ "OTEL_RESOURCE_ATTRIBUTES", "service.name=env-var-service" }
			}).Build(),
			new ElasticOpenTelemetryOptions(),
			new Hashtable(),
			false
		],
		[
			new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
			{
				{ "Elastic:OpenTelemetry:OpAmpEndpoint", "http://my-collector-endpoint-from-config.com" },
				{ "OTEL_RESOURCE_ATTRIBUTES", "service.name=env-var-service" }
			}).Build(),
			new ElasticOpenTelemetryOptions(),
			new Hashtable(),
			false
		],
		[
			new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
			{
				{ "Elastic:OpenTelemetry:OpAmpEndpoint", "http://my-collector-endpoint-from-config.com" },
				{ "Elastic:OpenTelemetry:OpAmpHeaders", "Authorization=ApiKey ABC123" }
			}).Build(),
			new ElasticOpenTelemetryOptions(),
			new Hashtable(),
			false
		],
		[
			new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
			{
				{ "OTEL_RESOURCE_ATTRIBUTES", "service.name=env-var-service" }
			}).Build(),
			new ElasticOpenTelemetryOptions()
			{
				OpAmpClientOptions = new OpAmpClientOptions()
				{
					Endpoint = "http://my-collector-endpoint-from-options.com",
					Headers = "Authorization=ApiKey ABC123"
				}
			},
			new Hashtable(),
			true
		],
		[
			new ConfigurationBuilder().Build(),
			new ElasticOpenTelemetryOptions()
			{
				OpAmpClientOptions = new OpAmpClientOptions()
				{
					Endpoint = "http://my-collector-endpoint-from-options.com",
					Headers = "Authorization=ApiKey ABC123"
				}
			},
			new Hashtable
			{
				{ OTEL_RESOURCE_ATTRIBUTES, "service.name=env-var-service" }
			},
			true
		],
		[
			new ConfigurationBuilder().Build(),
			new ElasticOpenTelemetryOptions()
			{
				OpAmpClientOptions = new OpAmpClientOptions()
				{
					Endpoint = "http://my-collector-endpoint-from-options.com"
				}
			},
			new Hashtable
			{
				{ OTEL_RESOURCE_ATTRIBUTES, "service.name=env-var-service" }
			},
			false
		],
		[
			new ConfigurationBuilder().Build(),
			new ElasticOpenTelemetryOptions()
			{
				OpAmpClientOptions = new OpAmpClientOptions()
				{
					Endpoint = "http://my-collector-endpoint-from-options.com"
				}
			},
			new Hashtable
			{
				{ OTEL_RESOURCE_ATTRIBUTES, "service.name=env-var-service" }
			},
			false
		],
		[
			new ConfigurationBuilder().Build(),
			new ElasticOpenTelemetryOptions()
			{
				OpAmpClientOptions = new OpAmpClientOptions
				{
					Headers = "Authorization=ApiKey ABC123"
				}
			},
			new Hashtable
			{
				{ OTEL_RESOURCE_ATTRIBUTES, "service.name=env-var-service" }
			},
			false
		],
		[
			new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
			{
				{ "Elastic:OpenTelemetry:OpAmpEndpoint", "http://my-collector-endpoint-from-config.com" },
			}).Build(),
			new ElasticOpenTelemetryOptions()
			{
				OpAmpClientOptions = new OpAmpClientOptions
				{
					Headers = "Authorization=ApiKey ABC123"
				}
			},
			new Hashtable
			{
				{ OTEL_RESOURCE_ATTRIBUTES, "service.name=env-var-service" }
			},
			true
		],
		[
			new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
			{
				{ "Elastic:OpenTelemetry:OpAmpHeaders", "Authorization=ApiKey ABC123" },
			}).Build(),
			new ElasticOpenTelemetryOptions()
			{
				OpAmpClientOptions = new OpAmpClientOptions
				{
					Endpoint = "http://my-collector-endpoint-from-options.com"
				}
			},
			new Hashtable
			{
				{ OTEL_RESOURCE_ATTRIBUTES, "service.name=env-var-service" }
			},
			true
		],
		[
			new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
			{
				{ "Elastic:OpenTelemetry:OpAmpHeaders", "NoAuth=True" },
			}).Build(),
			new ElasticOpenTelemetryOptions()
			{
				OpAmpClientOptions = new OpAmpClientOptions
				{
					Endpoint = "http://my-collector-endpoint-from-options.com"
				}
			},
			new Hashtable
			{
				{ OTEL_RESOURCE_ATTRIBUTES, "service.name=env-var-service" }
			},
			false
		],
		[
			new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
			{
				{ "Elastic:OpenTelemetry:OpAmpHeaders", "Authorization=ApiKey ABC123" },
			}).Build(),
			new ElasticOpenTelemetryOptions()
			{
				OpAmpClientOptions = new OpAmpClientOptions
				{
					Endpoint = "http://my-collector-endpoint-from-options.com"
				}
			},
			new Hashtable
			{
				{ OTEL_RESOURCE_ATTRIBUTES, "service.version=1.0.0" }
			},
			false
		]
	];

	[Fact]
	public void OpAmpEndpoint_EnvVarsTakePrecedenceOverIConfiguration()
	{
		const string expectedEndpoint = "http://my-collector-endpoint-from-env-vars.com";

		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				{ "ELASTIC_OTEL_OPAMP_ENDPOINT", "http://my-collector-endpoint-from-iconfiguration.com" }
			})
			.Build();

		var environmentVariables = new Hashtable
		{
			{ ELASTIC_OTEL_OPAMP_ENDPOINT, expectedEndpoint }
		};

		var sut = new CompositeElasticOpenTelemetryOptions(config, environmentVariables);

		Assert.Equal(expectedEndpoint, sut.OpAmpEndpoint);
	}

	[Fact]
	public void ResourceAttributes_CanBeSetFromIConfiguration()
	{
		const string expectedServiceName = "my-service";
		const string expectedServiceVersion = "1.0.0";
		const string expectedAttributes = $"service.name={expectedServiceName},service.version={expectedServiceVersion}";

		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				{ "OTEL_RESOURCE_ATTRIBUTES", expectedAttributes }
			})
			.Build();

		// The endpoint and headers are required to enable OpAMP
		var sut = new CompositeElasticOpenTelemetryOptions(config, new ElasticOpenTelemetryOptions
		{
			OpAmpClientOptions = new OpAmpClientOptions()
			{
				Endpoint = "http://localhost",
				Headers = "Authorization=ApiKey ABC123"
			}
		});

		Assert.Equal(expectedAttributes, sut.ResourceAttributes);

		Assert.Null(sut.ServiceName);
		Assert.Null(sut.ServiceVersion);

		sut.IsOpAmpEnabled();

		Assert.Equal(expectedServiceName, sut.ServiceName);
		Assert.Equal(expectedServiceVersion, sut.ServiceVersion);
	}

	[Fact]
	public void EnvironmentVariables_AreUsedWhenSet()
	{
		const string expectedLogDirectory = "C:\\EnvVar";
		const string expectedOpAmpEndpoint = "http://my-collector-endpoint-from-env-vars.com";
		const string expectedResourceAttributes = "service.name=env-var-service";
		const string expectedLogTargets = "file";
		const string expectedOpAmpHeaders = "api-key=env-var-api-key";

		var beforeLogDirectory = Environment.GetEnvironmentVariable(OTEL_DOTNET_AUTO_LOG_DIRECTORY);
		var beforeLogLevel = Environment.GetEnvironmentVariable(OTEL_LOG_LEVEL);
		var beforeSkipOtlpExporter = Environment.GetEnvironmentVariable(ELASTIC_OTEL_SKIP_OTLP_EXPORTER);
		var beforeSkipAssemblyScanning = Environment.GetEnvironmentVariable(ELASTIC_OTEL_SKIP_ASSEMBLY_SCANNING);
		var beforeOpAmpEndpoint = Environment.GetEnvironmentVariable(ELASTIC_OTEL_OPAMP_ENDPOINT);
		var beforeResourceAttributes = Environment.GetEnvironmentVariable(OTEL_RESOURCE_ATTRIBUTES);
		var beforeTargets = Environment.GetEnvironmentVariable(ELASTIC_OTEL_LOG_TARGETS);
		var beforeOpAmpHeaders = Environment.GetEnvironmentVariable(ELASTIC_OTEL_OPAMP_HEADERS);

		try
		{
			Environment.SetEnvironmentVariable(OTEL_DOTNET_AUTO_LOG_DIRECTORY, expectedLogDirectory);
			Environment.SetEnvironmentVariable(OTEL_LOG_LEVEL, "Debug");
			Environment.SetEnvironmentVariable(ELASTIC_OTEL_SKIP_OTLP_EXPORTER, "true");
			Environment.SetEnvironmentVariable(ELASTIC_OTEL_SKIP_ASSEMBLY_SCANNING, "true");
			Environment.SetEnvironmentVariable(ELASTIC_OTEL_OPAMP_ENDPOINT, expectedOpAmpEndpoint);
			Environment.SetEnvironmentVariable(OTEL_RESOURCE_ATTRIBUTES, expectedResourceAttributes);
			Environment.SetEnvironmentVariable(ELASTIC_OTEL_LOG_TARGETS, expectedLogTargets);
			Environment.SetEnvironmentVariable(ELASTIC_OTEL_OPAMP_HEADERS, expectedOpAmpHeaders);

			var sut = new CompositeElasticOpenTelemetryOptions();

			Assert.Equal(expectedLogDirectory, sut.LogDirectory);
			Assert.Equal(LogLevel.Debug, sut.LogLevel);
			Assert.True(sut.SkipOtlpExporter);
			Assert.True(sut.SkipInstrumentationAssemblyScanning);
			Assert.Equal(expectedOpAmpEndpoint, sut.OpAmpEndpoint);
			Assert.Equal(expectedResourceAttributes, sut.ResourceAttributes);
			Assert.Equal(expectedLogTargets, sut.LogTargets.ToString(), ignoreCase: true);
			Assert.Equal(expectedOpAmpHeaders, sut.OpAmpHeaders);
		}
		finally
		{
			Environment.SetEnvironmentVariable(OTEL_DOTNET_AUTO_LOG_DIRECTORY, beforeLogDirectory);
			Environment.SetEnvironmentVariable(OTEL_LOG_LEVEL, beforeLogLevel);
			Environment.SetEnvironmentVariable(ELASTIC_OTEL_SKIP_OTLP_EXPORTER, beforeSkipOtlpExporter);
			Environment.SetEnvironmentVariable(ELASTIC_OTEL_SKIP_ASSEMBLY_SCANNING, beforeSkipAssemblyScanning);
			Environment.SetEnvironmentVariable(ELASTIC_OTEL_OPAMP_ENDPOINT, beforeOpAmpEndpoint);
			Environment.SetEnvironmentVariable(OTEL_RESOURCE_ATTRIBUTES, beforeResourceAttributes);
			Environment.SetEnvironmentVariable(ELASTIC_OTEL_LOG_TARGETS, beforeTargets);
			Environment.SetEnvironmentVariable(ELASTIC_OTEL_OPAMP_HEADERS, beforeOpAmpHeaders);
		}
	}
}
