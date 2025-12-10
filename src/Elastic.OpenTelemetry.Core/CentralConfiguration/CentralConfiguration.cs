// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections.Concurrent;
using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenTelemetry.OpAmp.Client;
using OpenTelemetry.OpAmp.Client.Settings;

namespace Elastic.OpenTelemetry.Core;

internal sealed class CentralConfigurationOptions
{
	internal CentralConfigurationOptions()
	{
		var opAmpEndpoint = Environment.GetEnvironmentVariable(EnvironmentVariables.ELASTIC_OTEL_OPAMP_ENDPOINT);
		var resourceAttributes = Environment.GetEnvironmentVariable(EnvironmentVariables.OTEL_RESOURCE_ATTRIBUTES);

		if (string.IsNullOrEmpty(opAmpEndpoint) || string.IsNullOrEmpty(resourceAttributes))
		{
			return;
		}

		var serviceName = string.Empty;
		var serviceVersion = string.Empty;

		// TODO - Optimise parsing
		var attributes = resourceAttributes.Split(',', StringSplitOptions.RemoveEmptyEntries);

		foreach (var attribute in attributes)
		{
			if (serviceName != string.Empty && serviceVersion != string.Empty)
				break;

			if (!string.IsNullOrEmpty(attribute))
			{
				var keyAndValue = attribute.Split('=');

				if (keyAndValue.Length != 2)
					continue;

				if (keyAndValue[0] == "service.name")
				{
					serviceName = keyAndValue[1];
					continue;
				}

				if (keyAndValue[0] == "service.version")
				{
					serviceVersion = keyAndValue[1];
					continue;
				}
			}
		}
	}

	internal string OpAmpEndpoint { get; init; } = string.Empty;
	internal string ServiceName { get; init; } = string.Empty;
	internal string ServiceVersion { get; init; } = string.Empty;

	internal bool IsEnabled => !string.IsNullOrEmpty(OpAmpEndpoint) && !string.IsNullOrEmpty(ServiceName);
}

internal sealed record class RemoteConfiguration(LogLevel LogLevel);

internal interface ICentralConfigurationSubscriber
{
	void OnConfiguration(RemoteConfiguration remoteConfiguration);
}

internal sealed class CentralConfiguration : IDisposable
{
	private readonly CompositeLogger _logger;
	private readonly OpAmpClient _client;
	private readonly ConcurrentBag<ICentralConfigurationSubscriber> _subscribers = [];
	private readonly CancellationTokenSource? _cts;
	private bool _disposed;

	internal CentralConfiguration(CentralConfigurationOptions options, CompositeLogger logger)
	{
		_logger = logger;
		_client = new OpAmpClient(opts =>
		{
			opts.ServerUrl = new Uri(options.OpAmpEndpoint);
			opts.ConnectionType = ConnectionType.WebSocket;

			// Add custom resources to help the server identify your client.
			opts.Identification.AddIdentifyingAttribute("application.name", options.ServiceName);

			if (options.ServiceVersion != string.Empty)
				opts.Identification.AddIdentifyingAttribute("application.version", options.ServiceVersion);

			opts.Heartbeat.IsEnabled = false;
		});

		_cts = new CancellationTokenSource();

		_cts.Token.Register(async () =>
		{
			_logger.LogInformation("CentralConfiguration worker thread is stopping.");

			await _client.StopAsync().ConfigureAwait(false);
		});

		Task.Run(() => WorkerLoopAsync(_cts.Token));
	}

	private async Task WorkerLoopAsync(CancellationToken cancellationToken)
	{
		try
		{
			await _client.StartAsync(cancellationToken).ConfigureAwait(false);
			
			while (!cancellationToken.IsCancellationRequested)
			{
				// Example: poll for configuration changes or handle responses
				// This is a placeholder for actual response handling logic
				// You may need to subscribe to events or poll the client

				await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "CentralConfiguration worker thread encountered an error");
		}
	}

	internal void Subscribe(ICentralConfigurationSubscriber subscriber) => _subscribers.Add(subscriber);

	private void Dispose(bool disposing)
	{
		if (!_disposed)
		{
			if (disposing)
			{
				_cts?.Cancel();
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
}
