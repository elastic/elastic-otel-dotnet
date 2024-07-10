// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Configuration;
using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.Diagnostics.Logging;

/// <summary>
/// A composite logger for use inside the distribution which logs to the <see cref="Logging.FileLogger"/>
/// and optionally an additional <see cref="ILogger"/>.
/// </summary>
/// <remarks>
/// If disposed, triggers disposal of the <see cref="Logging.FileLogger"/>.
/// </remarks>
internal sealed class CompositeLogger(ElasticOpenTelemetryBuilderOptions options) : IDisposable, IAsyncDisposable, ILogger
{
	public const string LogCategory = "Elastic.OpenTelemetry";

	public FileLogger FileLogger { get; } = new(options.DistroOptions);
	public StandardOutLogger ConsoleLogger { get; } = new(options.DistroOptions);

	private ILogger? _additionalLogger = options.Logger;
	private bool _isDisposed;

	public void Dispose()
	{
		_isDisposed = true;
		if (_additionalLogger is IDisposable ad)
			ad.Dispose();
		FileLogger.Dispose();
	}

	public async ValueTask DisposeAsync()
	{
		_isDisposed = true;
		if (_additionalLogger is IAsyncDisposable ad)
			await ad.DisposeAsync().ConfigureAwait(false);
		await FileLogger.DisposeAsync().ConfigureAwait(false);
	}

	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		if (_isDisposed)
			return;

		if (FileLogger.IsEnabled(logLevel))
			FileLogger.Log(logLevel, eventId, state, exception, formatter);

		if (ConsoleLogger.IsEnabled(logLevel))
			ConsoleLogger.Log(logLevel, eventId, state, exception, formatter);

		if (_additionalLogger == null)
			return;

		if (_additionalLogger.IsEnabled(logLevel))
			_additionalLogger.Log(logLevel, eventId, state, exception, formatter);
	}

	public bool LogFileEnabled => FileLogger.FileLoggingEnabled;
	public string LogFilePath => FileLogger.LogFilePath ?? string.Empty;

	public void SetAdditionalLogger(ILogger? logger) => _additionalLogger ??= logger;

	public bool IsEnabled(LogLevel logLevel) => ConsoleLogger.IsEnabled(logLevel) || FileLogger.IsEnabled(logLevel) || (_additionalLogger?.IsEnabled(logLevel) ?? false);

	public IDisposable BeginScope<TState>(TState state) where TState : notnull =>
		new CompositeDisposable(FileLogger.BeginScope(state), _additionalLogger?.BeginScope(state));

	private class CompositeDisposable(params IDisposable?[] disposables) : IDisposable
	{
		public void Dispose()
		{
			foreach (var disposable in disposables)
				disposable?.Dispose();
		}
	}
}
