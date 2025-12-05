// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics.CodeAnalysis;
using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Diagnostics;
using OpenTelemetry.OpAmp.Client;
using OpenTelemetry.OpAmp.Client.Settings;

namespace Elastic.OpenTelemetry.Core.OpAmp;

internal record class CentralConfigurationOptions(string OpAmpEndpoint, string ServiceName, string ServiceVersion)
{
}

internal sealed class RemoteConfiguration
{

}

internal sealed class ElasticCentralConfiguration : IDisposable
{
	private static readonly SemaphoreSlim Semaphore = new(1, 1);
	private static bool IsConfigured = false;

	private readonly CompositeLogger _logger;
	private readonly OpAmpClient _client;
	private bool _disposed;

	private ElasticCentralConfiguration(CentralConfigurationOptions options, CompositeLogger logger)
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

		IsConfigured = true;
	}



	internal static bool TryCreate(CompositeLogger logger, [NotNullWhen(true)] out ElasticCentralConfiguration? config)
	{
		config = null;

		if (IsConfigured)
			return false;

		Semaphore.Wait();

		try
		{
			if (IsConfigured)
				return false;

			var opAmpEndpoint = Environment.GetEnvironmentVariable(EnvironmentVariables.ELASTIC_OTEL_OPAMP_ENDPOINT);
			var resourceAttributes = Environment.GetEnvironmentVariable(EnvironmentVariables.OTEL_RESOURCE_ATTRIBUTES);

			if (string.IsNullOrEmpty(opAmpEndpoint) || string.IsNullOrEmpty(resourceAttributes))
			{
				return false;
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

			var options = new CentralConfigurationOptions(opAmpEndpoint, serviceName, serviceVersion);
			config = new ElasticCentralConfiguration(options, logger);

			return true;
		}
		finally
		{
			Semaphore.Release();
		}
	}

	private void Dispose(bool disposing)
	{
		if (!_disposed)
		{
			if (disposing)
			{
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
