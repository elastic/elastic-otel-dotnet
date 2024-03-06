// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.Diagnostics.Logging;

/// <summary> A composite logger for use inside the agent, will dispose <see cref="Logging.FileLogger"/> </summary>
internal sealed class AgentCompositeLogger(ILogger? additionalLogger) : IDisposable, IAsyncDisposable, ILogger
{
	public FileLogger FileLogger { get; } = new();

	private bool _isDisposed;

	/// <summary> TODO </summary>
	public void Dispose()
	{
		_isDisposed = true;
		FileLogger.Dispose();
	}

	/// <summary> TODO </summary>
	public ValueTask DisposeAsync()
	{
		_isDisposed = true;
		return FileLogger.DisposeAsync();
	}

	/// <summary> TODO </summary>
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

	/// <summary> TODO </summary>
	public void SetAdditionalLogger(ILogger? logger) => additionalLogger ??= logger;

	/// <summary> TODO </summary>
	public bool IsEnabled(LogLevel logLevel) => FileLogger.IsEnabled(logLevel) || (additionalLogger?.IsEnabled(logLevel) ?? false);

	/// <summary> TODO </summary>
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
