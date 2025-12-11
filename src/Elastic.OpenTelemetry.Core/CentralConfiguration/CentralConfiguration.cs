// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenTelemetry.OpAmp.Client;
using OpenTelemetry.OpAmp.Client.Settings;

namespace Elastic.OpenTelemetry.Core;

internal sealed record class RemoteConfiguration(LogLevel LogLevel);

internal interface ICentralConfigurationSubscriber
{
	void OnConfiguration(RemoteConfiguration remoteConfiguration);
}

internal sealed class CentralConfiguration : IDisposable, IAsyncDisposable
{
	private const int OpAmpBlockingStartTimeoutMilliseconds = 500;

	private readonly CompositeLogger _logger;
	private readonly OpAmpClient _client;
	private readonly ConcurrentBag<ICentralConfigurationSubscriber> _subscribers = [];

	private bool _disposed;

	private CentralConfiguration(CompositeElasticOpenTelemetryOptions options, CompositeLogger logger)
	{
		_logger = logger;

		// TODO - Configure HttpClient once supported by  OpAmpClient.
		// TODO - Subscribe to configuration updates and notify subscribers once supported by OpAmpClient.

		_client = new OpAmpClient(opts =>
		{
			opts.ServerUrl = new Uri(options.OpAmpEndpoint!);
			opts.ConnectionType = ConnectionType.Http;

			// Add custom resources to help the server identify your client.
			opts.Identification.AddIdentifyingAttribute("application.name", options.ServiceName!);

			if (!string.IsNullOrEmpty(options.ServiceVersion))
				opts.Identification.AddIdentifyingAttribute("application.version", options.ServiceVersion!);

			opts.Heartbeat.IsEnabled = false;
		});
	}

	internal bool IsStarted { get; private set; }

	private async Task StartAsync()
	{
		if (IsStarted)
		{
			return;
		}

		await _client.StartAsync().ConfigureAwait(false);

		IsStarted = true;
	}

	internal static bool TryCreateAndStart(CompositeElasticOpenTelemetryOptions options, CompositeLogger logger, [NotNullWhen(true)] out CentralConfiguration? centralConfig)
	{
		centralConfig = null;

		if (options.IsOpAmpEnabled() is false)
		{
			return false;
		}

		logger.LogDebug("Central Configuration is enabled. OpAmp Endpoint: {OpAmpEndpoint}, Service Name: {ServiceName}, Service Version: {ServiceVersion}",
			options.OpAmpEndpoint,
			options.ServiceName,
			options.ServiceVersion);

		centralConfig = new CentralConfiguration(options, logger);

		var startTask = centralConfig.StartAsync();

		// Wait for up to 500ms for the task to complete
		var completedInTime = startTask.Wait(TimeSpan.FromMilliseconds(OpAmpBlockingStartTimeoutMilliseconds));

		if (completedInTime)
		{
			// Task completed within the timeout
			if (startTask.IsFaulted)
			{
				logger.LogError(startTask.Exception, "Failed to start Central Configuration client.");

				centralConfig = null;
				return false;
			}

			logger.LogInformation("Central Configuration client started successfully.");
			return true;
		}

		// Task did not complete within 500ms, continue asynchronously
		logger.LogDebug("Central Configuration client is starting asynchronously (exceeded 500ms wait).");

		_ = startTask.ContinueWith(task =>
		{
			if (task.IsFaulted)
			{
				logger.LogError(task.Exception, "Failed to start Central Configuration client.");
			}
			else
			{
				logger.LogInformation("Central Configuration client started successfully.");
			}
		}, TaskScheduler.Default);

		return true;
	}

	internal void Subscribe(ICentralConfigurationSubscriber subscriber)
	{
		_subscribers.Add(subscriber);
		_logger.LogDebug("Subscriber of type '{SubscriberType}' added to Central Configuration. Total subscribers: {SubscriberCount}", subscriber.GetType().Name, _subscribers.Count);
	}

	private void Dispose(bool disposing)
	{
		if (!_disposed)
		{
			if (disposing)
			{
				_subscribers.Clear();
				_client.Dispose();
			}

			_disposed = true;
		}
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	public ValueTask DisposeAsync()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);

#if NET
		return ValueTask.CompletedTask;
#else
		return new ValueTask(Task.CompletedTask);
#endif
	}
}
