// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.OpAmp.Abstractions;
using Microsoft.Extensions.Logging;
using OpenTelemetry.OpAmp.Client;

#if NETFRAMEWORK
using System.Net.Http;
#endif

namespace Elastic.OpenTelemetry.OpAmp
{
	internal sealed class ElasticOpAmpClient : IOpAmpClient
	{
		private readonly OpAmpClient _innerClient;
		private readonly HttpClient _httpClient;
		private readonly RemoteConfigMessageListener _remoteConfigMessageListener;
		private readonly ILogger _logger;
		private int _disposed;

		/// <remarks>
		/// WARNING: This constructor is called by <c>ElasticOpAmpClientFactory.Create</c>,
		/// which in turn is invoked by reflection from
		/// <c>OpAmpIsolatedLoadContext.CreateOpAmpClientInstance</c>.
		/// Any signature change must be mirrored in <see cref="IOpAmpClientFactory.Create"/>,
		/// which will produce compiler errors at every call site and implementation.
		/// </remarks>
		public ElasticOpAmpClient(ILogger logger, string endPoint, string headers, string serviceName, string? serviceVersion, string userAgent)
		{
			_httpClient = OpAmpClientConfiguration.CreateHttpClient(headers, userAgent);
			try
			{
				_innerClient = new OpAmpClient(
					OpAmpClientConfiguration.GetConfigurationAction(
						endPoint, serviceName, serviceVersion, _httpClient));
				_remoteConfigMessageListener = new(logger);
				_innerClient.Subscribe(_remoteConfigMessageListener);
			}
			catch
			{
				_httpClient.Dispose();
				throw;
			}
			_logger = logger;
		}

		public Task StartAsync(CancellationToken cancellationToken = default)
		{
			_logger.LogStartingOpAmpClient(nameof(ElasticOpAmpClient));
			return _innerClient.StartAsync(cancellationToken);
		}

		public Task StopAsync(CancellationToken cancellationToken = default)
		{
			_logger.LogStoppingOpAmpClient(nameof(ElasticOpAmpClient));
			return _innerClient.StopAsync(cancellationToken);
		}

		public void SubscribeToRemoteConfigMessages(IOpAmpRemoteConfigMessageSubscriber subscriber)
		{
			_logger.LogSubscribingToRemoteConfig(nameof(ElasticOpAmpClient), subscriber.GetType().FullName);
			_remoteConfigMessageListener.Subscribe(subscriber);
		}

		/// <remarks>
		/// Disposes both the inner <see cref="OpAmpClient"/> and the <see cref="HttpClient"/> to
		/// work around the upstream transport disposal gap where <c>OpAmpClient.Dispose()</c> does
		/// not dispose the <see cref="HttpClient"/> obtained from
		/// <c>OpAmpClientSettings.HttpClientFactory</c>.
		/// </remarks>
		void IDisposable.Dispose()
		{
			if (Interlocked.Exchange(ref _disposed, 1) != 0)
				return;
			_logger.LogDisposingElasticOpAmpClient(nameof(ElasticOpAmpClient));
			// Dispose inner client first — it may still reference _httpClient during its disposal.
			_innerClient.Dispose();
			_httpClient.Dispose();
		}
	}
}
