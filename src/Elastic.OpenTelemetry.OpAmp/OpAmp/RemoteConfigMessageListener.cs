// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.OpAmp.Abstractions;
using Microsoft.Extensions.Logging;
using OpenTelemetry.OpAmp.Client.Listeners;
using OpenTelemetry.OpAmp.Client.Messages;

namespace Elastic.OpenTelemetry.OpAmp
{
	internal sealed class RemoteConfigMessageListener(ILogger logger) : IOpAmpListener<RemoteConfigMessage>
	{
		private IOpAmpRemoteConfigMessageSubscriber[] _subscribers = [];
		private readonly ILogger _logger = logger;

		public void HandleMessage(RemoteConfigMessage message)
		{
			_logger.LogReceivedRemoteConfig(nameof(RemoteConfigMessageListener));

			if (!message.AgentConfigMap.TryGetValue("elastic", out var config))
			{
				_logger.LogNoElasticConfigKey(nameof(RemoteConfigMessageListener));
				return;
			}

			if (config.ContentType != "application/json")
			{
				_logger.LogConfigNotJsonFormat(nameof(RemoteConfigMessageListener));
				return;
			}

			var body = config.Body;

			var parser = new CentralConfigJsonParser(body);

			if (!parser.TryParseLogLevel(out var logLevel))
			{
				_logger.LogNoValidLogLevel(nameof(RemoteConfigMessageListener));
				return;
			}

			_logger.LogExtractedLogLevel(nameof(RemoteConfigMessageListener), logLevel!);

			var mapped = new ElasticRemoteConfig(logLevel);

			// Paired with Interlocked.CompareExchange in Subscribe
			var subscribers = Volatile.Read(ref _subscribers);

			_logger.LogNotifyingSubscribers(nameof(RemoteConfigMessageListener), subscribers.Length);

			foreach (var subscriber in subscribers)
				subscriber.HandleMessage(mapped);
		}

		internal void Subscribe(IOpAmpRemoteConfigMessageSubscriber subscriber)
		{
			// There is a small overhead to this approach, but it is thread-safe and we don't expect a large number of subscribers, so it should be fine.

			while (true)
			{
				// Paired with Interlocked.CompareExchange below
				var current = Volatile.Read(ref _subscribers);
				var updated = new IOpAmpRemoteConfigMessageSubscriber[current.Length + 1];
				Array.Copy(current, updated, current.Length);
				updated[current.Length] = subscriber;

				if (Interlocked.CompareExchange(ref _subscribers, updated, current) == current)
				{
					_logger.LogSubscriberAdded(nameof(RemoteConfigMessageListener), updated.Length);
					break;
				}
			}
		}
	}
}
