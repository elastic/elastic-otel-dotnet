// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.Tests;

public class FileLoggerTests : IDisposable
{
	private readonly FileLogger _logger;

	public FileLoggerTests()
	{
		// Use a path with illegal characters to force FileStream creation to fail.
		// This ensures the catch block in the FileLogger constructor runs,
		// setting _fileLoggingEnabled = false.
		var options = new ElasticOpenTelemetryOptions
		{
			LogDirectory = Path.Combine(Path.GetTempPath(), "edot-test-\0-invalid"),
			LogLevel = LogLevel.Information,
			LogTargets = LogTargets.File
		};
		var compositeOptions = new CompositeElasticOpenTelemetryOptions(options);
		_logger = new FileLogger(compositeOptions);
	}

	public void Dispose() => _logger.Dispose();

	[Fact]
	public void FileLoggingEnabled_RemainsFalse_AfterInitializationFailure() => Assert.False(_logger.FileLoggingEnabled);

	[Fact]
	public void IsEnabled_RemainsFalse_AfterInitializationFailure()
	{
		// Call IsEnabled multiple times to verify re-evaluation doesn't flip it back to true
		Assert.False(_logger.IsEnabled(LogLevel.Information));
		Assert.False(_logger.IsEnabled(LogLevel.Information));
		Assert.False(_logger.IsEnabled(LogLevel.Critical));
	}
}
