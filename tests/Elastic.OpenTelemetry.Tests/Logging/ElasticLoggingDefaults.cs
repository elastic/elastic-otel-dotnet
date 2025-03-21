// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using OpenTelemetry;
using OpenTelemetry.Logs;
using Xunit.Abstractions;

namespace Elastic.OpenTelemetry.Tests.Logging;

public class ElasticLoggingDefaults(ITestOutputHelper output)
{
	private readonly ITestOutputHelper _output = output;

	[Fact]
	public async Task FormattedMessageAndScopesOptions_AreEnabled()
	{
		var exportedItems = new List<LogRecord>();

		var host = Host.CreateDefaultBuilder();
		host.ConfigureServices(s =>
		{
			var options = new ElasticOpenTelemetryOptions()
			{
				SkipOtlpExporter = true,
				AdditionalLogger = new TestLogger(_output)
			};

			s.AddElasticOpenTelemetry(options)
				.WithLogging(lpb => lpb.AddInMemoryExporter(exportedItems));
		});

		var ctx = new CancellationTokenRegistration();

		using (var app = host.Build())
		{
			_ = app.RunAsync(ctx.Token);

			var factory = app.Services.GetRequiredService<ILoggerFactory>();
			var logger = factory.CreateLogger("Test");

			using (logger.BeginScope(new List<KeyValuePair<string, object>>
			{
				new("customData", "aCustomValue"),
			}))
			{
				logger.LogWarning("This is a {WhatAmI}", "warning");
			}

			await ctx.DisposeAsync();
		}

		var logRecord = exportedItems.Last();

		Assert.Equal("This is a warning", logRecord.FormattedMessage);

		logRecord.ForEachScope<object?>((scope, _) =>
		{
			var values = scope.Scope as IEnumerable<KeyValuePair<string, object>>;

			Assert.NotNull(values);
			var entry = Assert.Single(values);

			Assert.Equal("customData", entry.Key);
			Assert.Equal("aCustomValue", entry.Value);
		}, null);
	}
}
