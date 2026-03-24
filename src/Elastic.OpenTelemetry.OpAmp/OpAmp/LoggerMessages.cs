// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.OpAmp
{
	internal static partial class LoggerMessages
	{
		// NOTES:
		// - The IDs and EventNames should ideally not change to ensure consistency in log querying.
		// - EventIds start at 200 to avoid conflicts with Core Diagnostics LoggerMessages (1-60)
		//   and Core Configuration LoggerMessages (100-199).

		// RemoteConfigMessageListener messages

		[LoggerMessage(EventId = 200, EventName = "ReceivedRemoteConfig", Level = LogLevel.Debug,
			Message = "{ClassName}: Received remote config message.")]
		internal static partial void LogReceivedRemoteConfig(this ILogger logger, string className);

		[LoggerMessage(EventId = 202, EventName = "NoElasticConfigKey", Level = LogLevel.Debug,
			Message = "{ClassName}: Remote config message does not contain 'elastic' config. Skipping.")]
		internal static partial void LogNoElasticConfigKey(this ILogger logger, string className);

		[LoggerMessage(EventId = 203, EventName = "ConfigNotJsonFormat", Level = LogLevel.Debug,
			Message = "{ClassName}: Remote config message 'elastic' config is not in expected JSON format. Skipping.")]
		internal static partial void LogConfigNotJsonFormat(this ILogger logger, string className);

		[LoggerMessage(EventId = 204, EventName = "NoValidLogLevel", Level = LogLevel.Debug,
			Message = "{ClassName}: Remote config message 'elastic' config does not contain a valid 'log_level' property. Skipping log level extraction.")]
		internal static partial void LogNoValidLogLevel(this ILogger logger, string className);

		[LoggerMessage(EventId = 205, EventName = "ExtractedLogLevel", Level = LogLevel.Debug,
			Message = "{ClassName}: Extracted log level '{LogLevel}' from remote config message.")]
		internal static partial void LogExtractedLogLevel(this ILogger logger, string className, string logLevel);

		[LoggerMessage(EventId = 206, EventName = "NotifyingSubscribers", Level = LogLevel.Debug,
			Message = "{ClassName}: Notifying {SubscriberCount} subscribers of new remote config message.")]
		internal static partial void LogNotifyingSubscribers(this ILogger logger, string className, int subscriberCount);

		[LoggerMessage(EventId = 207, EventName = "SubscriberAdded", Level = LogLevel.Debug,
			Message = "{ClassName}: Subscriber added. Total subscribers: {SubscriberCount}.")]
		internal static partial void LogSubscriberAdded(this ILogger logger, string className, int subscriberCount);

		// ElasticOpAmpClient messages

		[LoggerMessage(EventId = 220, EventName = "StartingOpAmpClient", Level = LogLevel.Debug,
			Message = "{ClassName}: Starting OpAmp client.")]
		internal static partial void LogStartingOpAmpClient(this ILogger logger, string className);

		[LoggerMessage(EventId = 221, EventName = "StoppingOpAmpClient", Level = LogLevel.Debug,
			Message = "{ClassName}: Stopping OpAmp client.")]
		internal static partial void LogStoppingOpAmpClient(this ILogger logger, string className);

		[LoggerMessage(EventId = 222, EventName = "SubscribingToRemoteConfig", Level = LogLevel.Debug,
			Message = "{ClassName}: Subscribing to remote config messages with subscriber of type {SubscriberType}.")]
		internal static partial void LogSubscribingToRemoteConfig(this ILogger logger, string className, string? subscriberType);

		[LoggerMessage(EventId = 223, EventName = "DisposingElasticOpAmpClient", Level = LogLevel.Debug,
			Message = "{ClassName}: Disposing OpAmp client.")]
		internal static partial void LogDisposingElasticOpAmpClient(this ILogger logger, string className);
	}
}
