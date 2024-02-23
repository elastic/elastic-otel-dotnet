// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.Diagnostics.Logging;

/// <summary>
/// A logger that writes to both <see cref="FileLogger"/> and the user configured logger.
/// <para> Instances of this never dispose <see cref="FileLogger"/> so can be used anywhere even on short lived objects</para>
/// </summary>
public sealed class ScopedCompositeLogger<T>(ILogger? additionalLogger) : IDisposable, IAsyncDisposable, ILogger<T>
{
	private readonly FileLogger _fileLogger = FileLogger.Instance;

	/// <summary> TODO </summary>
	public void Dispose()
	{
		if (additionalLogger is IDisposable d)
			d.Dispose();
	}

	/// <summary> TODO </summary>
	public ValueTask DisposeAsync() => additionalLogger is IAsyncDisposable d ? d.DisposeAsync() : ValueTask.CompletedTask;

	/// <summary> TODO </summary>
	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		if (_fileLogger.IsEnabled(logLevel))
			_fileLogger.Log(logLevel, eventId, state, exception, formatter);

		if (additionalLogger == null) return;

		if (additionalLogger.IsEnabled(logLevel))
			additionalLogger.Log(logLevel, eventId, state, exception, formatter);
	}

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
