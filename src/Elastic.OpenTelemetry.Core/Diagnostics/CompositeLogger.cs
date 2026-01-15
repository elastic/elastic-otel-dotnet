// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.IO;
using System.Runtime.CompilerServices;
using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Core;
using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.Diagnostics;

/// <summary>
/// A composite logger for use inside the distribution which logs to the <see cref="Diagnostics.FileLogger"/>
/// and optionally an additional <see cref="ILogger"/>.
/// </summary>
/// <remarks>
/// If disposed, triggers disposal of the <see cref="Diagnostics.FileLogger"/>.
/// </remarks>
internal sealed class CompositeLogger : IDisposable, IAsyncDisposable, ILogger
{
	public const string LogCategory = "Elastic.OpenTelemetry";

	internal Guid InstanceId { get; } = Guid.NewGuid();

	public FileLogger FileLogger { get; }
	public StandardOutLogger ConsoleLogger { get; }

	private ILogger? _additionalLogger;
	private bool _isDisposed;

	public CompositeLogger(CompositeElasticOpenTelemetryOptions options)
	{
		if (BootstrapLogger.IsEnabled)
		{
			BootstrapLogger.LogWithStackTrace($"{nameof(CompositeLogger)}: Instance '{InstanceId}' created via ctor." +
				$"{Environment.NewLine}    Invoked with `{nameof(CompositeElasticOpenTelemetryOptions)}` instance '{options.InstanceId}'.");
		}

		FileLogger = new(options);
		ConsoleLogger = new(options);

		_additionalLogger = options.AdditionalLogger;
	}

	public void Dispose()
	{
		_isDisposed = true;
		_additionalLogger = null;
		FileLogger.Dispose();
	}

	public async ValueTask DisposeAsync()
	{
		_isDisposed = true;
		_additionalLogger = null;
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

		if (_additionalLogger is not null && _additionalLogger.IsEnabled(logLevel))
			_additionalLogger.Log(logLevel, eventId, state, exception, formatter);
	}

	public bool LogFileEnabled => FileLogger.FileLoggingEnabled;

	public string LogFilePath => FileLogger.LogFilePath ?? string.Empty;

	public void SetAdditionalLogger(ILogger logger, SdkActivationMethod activationMethod, ElasticOpenTelemetryComponents components)
	{
		if (HasAdditionalLogger)
			return;

		components.Logger.LogInformation("Added additional ILogger to composite logger.");

		_additionalLogger = logger;
		_additionalLogger.LogDistroPreamble(activationMethod, components);
	}

	internal bool HasAdditionalLogger => _additionalLogger is not null;

	public bool IsEnabled(LogLevel logLevel) =>
		ConsoleLogger.IsEnabled(logLevel) || FileLogger.IsEnabled(logLevel) || (_additionalLogger?.IsEnabled(logLevel) ?? false);

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
