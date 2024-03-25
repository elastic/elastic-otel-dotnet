// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.Diagnostics.Logging;

/// <summary>
/// A composite logger for use inside the distribution which logs to the <see cref="Logging.FileLogger"/>
/// and optionally an additional <see cref="ILogger"/>.
/// </summary>
/// <remarks>
/// If disposed, triggers disposal of the <see cref="Logging.FileLogger"/>.
/// </remarks>
internal sealed class CompositeLogger(ILogger? additionalLogger) : IDisposable, IAsyncDisposable, ILogger
{
	public FileLogger FileLogger { get; } = new();

	private bool _isDisposed;

	public void Dispose()
	{
		_isDisposed = true;
		FileLogger.Dispose();
	}

	public ValueTask DisposeAsync()
	{
		_isDisposed = true;
		return FileLogger.DisposeAsync();
	}

	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		if (_isDisposed)
			return;

		if (FileLogger.IsEnabled(logLevel))
			FileLogger.Log(logLevel, eventId, state, exception, formatter);

		if (additionalLogger == null)
			return;

		if (additionalLogger.IsEnabled(logLevel))
			additionalLogger.Log(logLevel, eventId, state, exception, formatter);
	}

	public bool LogFileEnabled => FileLogger.FileLoggingEnabled;
	public string LogFilePath => FileLogger.LogFilePath ?? string.Empty;

	public void SetAdditionalLogger(ILogger? logger) => additionalLogger ??= logger;

	public bool IsEnabled(LogLevel logLevel) => FileLogger.IsEnabled(logLevel) || (additionalLogger?.IsEnabled(logLevel) ?? false);

	public IDisposable BeginScope<TState>(TState state) where TState : notnull =>
		new CompositeDisposable(FileLogger.BeginScope(state), additionalLogger?.BeginScope(state));

	private class CompositeDisposable(params IDisposable?[] disposables) : IDisposable
	{
		public void Dispose()
		{
			foreach (var disposable in disposables)
				disposable?.Dispose();
		}
	}
}
