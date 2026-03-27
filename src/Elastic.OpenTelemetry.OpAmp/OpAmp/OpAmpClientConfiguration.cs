// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using OpenTelemetry.OpAmp.Client.Settings;

#if NETFRAMEWORK
using System.Net.Http;
#endif

namespace Elastic.OpenTelemetry.OpAmp
{
	internal static class OpAmpClientConfiguration
	{
		/// <summary>
		/// Creates and configures the <see cref="HttpClient"/> used for OpAMP communication.
		/// </summary>
		/// <remarks>
		/// The caller owns the returned <see cref="HttpClient"/> and is responsible for disposing it.
		/// This is necessary because <c>OpAmpClient.Dispose()</c> does not dispose the transport
		/// or the <see cref="HttpClient"/> obtained from <see cref="OpAmpClientSettings.HttpClientFactory"/>.
		/// TODO: Add upstream issue URL once filed so this workaround is traceable.
		/// </remarks>
		public static HttpClient CreateHttpClient(string headers, string userAgent)
		{
			var client = new HttpClient();
			client.DefaultRequestHeaders.Add("User-Agent", userAgent);
			var userHeaders = (headers ?? string.Empty).Split(',');
			foreach (var header in userHeaders)
			{
				var parts = header.Split(['='], 2);
				if (parts.Length == 2)
				{
					var key = parts[0].Trim();
					var value = parts[1].Trim();
					client.DefaultRequestHeaders.Add(key, value);
				}
			}
			return client;
		}

		/// <summary>
		/// Returns the configuration action for <see cref="OpAmpClientSettings"/>.
		/// </summary>
		/// <remarks>
		/// The <paramref name="httpClient"/> is passed to <see cref="OpAmpClientSettings.HttpClientFactory"/>
		/// as a single-use factory. The OpAMP client calls this factory exactly once during construction
		/// (verified in upstream <c>PlainHttpTransport</c> source) and reuses the returned instance for
		/// all communication. The factory is never called again on reconnect or retry.
		/// <para/>
		/// The caller retains ownership of the <see cref="HttpClient"/> and must dispose it after
		/// disposing the <c>OpAmpClient</c>.
		/// </remarks>
		public static Action<OpAmpClientSettings> GetConfigurationAction(
			string endPoint,
			string serviceName,
			string? serviceVersion,
			HttpClient httpClient) =>
				opts =>
				{
					opts.ServerUrl = new Uri(endPoint);
					opts.ConnectionType = ConnectionType.Http;
					opts.HttpClientFactory = () => httpClient;
					opts.Identification.AddIdentifyingAttribute("application.name", serviceName);
					if (!string.IsNullOrEmpty(serviceVersion))
						opts.Identification.AddIdentifyingAttribute("application.version", serviceVersion!);
					opts.Heartbeat.IsEnabled = false;
				};
	}
}
