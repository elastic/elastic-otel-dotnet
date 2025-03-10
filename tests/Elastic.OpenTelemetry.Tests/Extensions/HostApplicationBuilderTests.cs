// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Text;
using Elastic.OpenTelemetry.Core;

namespace Elastic.OpenTelemetry.Tests.Extensions;

public class HostApplicationBuilderTests
{
	[Fact]
	public void AddElasticOpenTelemetry_AppliesConfigAndOptions_InExpectedOrder()
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
								"SkipOtlpExporter": true,
								"SkipInstrumentationAssemblyScanning": true
							}
						}
					}
					""";

		var builder = Host.CreateApplicationBuilder();

		builder.Configuration.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)));
		builder.AddElasticOpenTelemetry(options);

		var app = builder.Build();

		var components = app.Services.GetRequiredService<ElasticOpenTelemetryComponents>();

		Assert.Equal(fileLogDirectory, components.Options.LogDirectory);
		Assert.Equal(LogLevel.Critical, components.Options.LogLevel);
		Assert.True(components.Options.SkipOtlpExporter);
		Assert.True(components.Options.SkipInstrumentationAssemblyScanning);
	}
}
