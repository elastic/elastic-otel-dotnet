// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Diagnostics.Logging;
using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.DependencyInjection;

/// <summary> A composite logger for use inside the agent, will dispose <see cref="FileLogger"/> </summary>
internal sealed class AgentCompositeLogger(ILogger? additionalLogger) : IDisposable, IAsyncDisposable, ILogger
{
	private readonly FileLogger _fileLogger = FileLogger.Instance;

	/// <summary> TODO </summary>
	public void Dispose() => _fileLogger.Dispose();

	/// <summary> TODO </summary>
	public ValueTask DisposeAsync() => _fileLogger.DisposeAsync();

	/// <summary> TODO </summary>
	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		_fileLogger.Log(logLevel, eventId, state, exception, formatter);
		additionalLogger?.Log(logLevel, eventId, state, exception, formatter);
	}

	public bool LogFileEnabled => _fileLogger.FileLoggingEnabled;
	public string LogFilePath => _fileLogger.LogFilePath ?? string.Empty;

	/// <summary> TODO </summary>
	public void SetAdditionalLogger(ILogger? logger) => additionalLogger ??= logger;

	/// <summary> TODO </summary>
	public bool IsEnabled(LogLevel logLevel) => _fileLogger.IsEnabled(logLevel) || (additionalLogger?.IsEnabled(logLevel) ?? false);

	/// <summary> TODO </summary>
	public IDisposable BeginScope<TState>(TState state) where TState : notnull =>
		new CompositeDisposable(_fileLogger.BeginScope(state), additionalLogger?.BeginScope(state));

	private class CompositeDisposable(params IDisposable?[] disposables) : IDisposable
	{
		public void Dispose()
		{
			foreach(var disposable in disposables)
				disposable?.Dispose();
		}
	}
}
