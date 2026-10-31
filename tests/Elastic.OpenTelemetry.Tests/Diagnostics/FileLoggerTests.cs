// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Text;
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

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Dispose_FlushesAllQueuedEntries(bool disposeAsync)
	{
		const int entryCount = 1000;

		var logDirectory = CreateLogDirectoryPath();

		try
		{
			var logger = new FileLogger(CreateOptions(logDirectory));
			Assert.True(logger.FileLoggingEnabled);

			for (var i = 0; i < entryCount; i++)
				logger.Log(LogLevel.Information, default, $"[entry-{i:D4}]", null, (s, _) => s);

			await DisposeLoggerAsync(logger, disposeAsync);

			var contents = File.ReadAllText(logger.LogFilePath!);

			var missing = Enumerable.Range(0, entryCount)
				.Where(i => !contents.Contains($"[entry-{i:D4}]"))
				.ToList();

			Assert.Empty(missing);
		}
		finally
		{
			DeleteDirectory(logDirectory);
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Dispose_WhenWriteTimesOut_DefersCleanupUntilWritingCompletes(bool disposeAsync)
	{
		var logDirectory = CreateLogDirectoryPath();
		using var stream = new BlockingFlushStream();
		var logger = new FileLogger(CreateOptions(logDirectory), TimeSpan.FromMilliseconds(100), _ => stream);

		try
		{
			Assert.True(logger.FileLoggingEnabled);

			logger.Log(LogLevel.Information, default, "first entry", null, (s, _) => s);
			logger.Log(LogLevel.Information, default, "second entry", null, (s, _) => s);
			await stream.WriteStarted.WaitAsync(TimeSpan.FromSeconds(10));

			// Dispose returns once the timeout elapses, but must not dispose the stream under the in-flight write.
			await DisposeLoggerAsync(logger, disposeAsync);
			Assert.False(stream.Disposed.IsCompleted);

			stream.AllowWrites();
			await stream.Disposed.WaitAsync(TimeSpan.FromSeconds(10));

			var contents = Encoding.UTF8.GetString(stream.ToArray());
			Assert.Contains("first entry", contents);
			Assert.Contains("second entry", contents);
		}
		finally
		{
			stream.AllowWrites();
			await logger.DisposeAsync();
			DeleteDirectory(logDirectory);
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Log_WhenWriteFails_DisablesLoggingAndDisposesCleanly(bool disposeAsync)
	{
		var logDirectory = CreateLogDirectoryPath();
		using var stream = new FailingFlushStream();
		var logger = new FileLogger(CreateOptions(logDirectory), TimeSpan.FromSeconds(10), _ => stream);

		try
		{
			Assert.True(logger.IsEnabled(LogLevel.Information));

			logger.Log(LogLevel.Information, default, "entry", null, (s, _) => s);

			// The writing task handles the failure itself and exits, rather than faulting.
			await logger.WritingTask.WaitAsync(TimeSpan.FromSeconds(10));
			Assert.False(logger.IsEnabled(LogLevel.Information));

			var exception = await Record.ExceptionAsync(() => DisposeLoggerAsync(logger, disposeAsync));

			Assert.Null(exception);
			Assert.False(stream.CanWrite);
		}
		finally
		{
			await logger.DisposeAsync();
			DeleteDirectory(logDirectory);
		}
	}

	[Fact]
	public void Log_AfterDispose_DoesNotThrow()
	{
		var logDirectory = CreateLogDirectoryPath();

		try
		{
			var logger = new FileLogger(CreateOptions(logDirectory));
			Assert.True(logger.FileLoggingEnabled);

			logger.Dispose();

			var exception = Record.Exception(() => logger.Log(LogLevel.Information, default, "after dispose", null, (s, _) => s));

			Assert.Null(exception);
		}
		finally
		{
			DeleteDirectory(logDirectory);
		}
	}

	private static string CreateLogDirectoryPath() => Path.Combine(Path.GetTempPath(), $"edot-test-{Guid.NewGuid():N}");

	private static CompositeElasticOpenTelemetryOptions CreateOptions(string logDirectory) =>
		new(new ElasticOpenTelemetryOptions
		{
			LogDirectory = logDirectory,
			LogLevel = LogLevel.Information,
			LogTargets = LogTargets.File
		});

	private static async Task DisposeLoggerAsync(FileLogger logger, bool disposeAsync)
	{
		if (disposeAsync)
			await logger.DisposeAsync();
		else
			logger.Dispose();
	}

	private static void DeleteDirectory(string logDirectory)
	{
		if (Directory.Exists(logDirectory))
			Directory.Delete(logDirectory, recursive: true);
	}

	private sealed class BlockingFlushStream : MemoryStream
	{
		private readonly TaskCompletionSource _writeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private readonly TaskCompletionSource _allowWrites = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private readonly TaskCompletionSource _disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);

		public Task WriteStarted => _writeStarted.Task;
		public Task Disposed => _disposed.Task;

		public void AllowWrites() => _allowWrites.TrySetResult();

		public override async Task FlushAsync(CancellationToken cancellationToken)
		{
			_writeStarted.TrySetResult();
			await _allowWrites.Task.WaitAsync(cancellationToken);
			await base.FlushAsync(cancellationToken);
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			_disposed.TrySetResult();
		}
	}

	// Simulates an I/O failure (e.g. disk full) on the asynchronous writes made by the writing task.
	private sealed class FailingFlushStream : MemoryStream
	{
		public override Task FlushAsync(CancellationToken cancellationToken) =>
			Task.FromException(new IOException("Simulated write failure."));
	}
}
