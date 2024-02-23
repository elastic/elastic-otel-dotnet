// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information
using Elastic.OpenTelemetry.Processors;
using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.Diagnostics
{
	internal static partial class LoggerMessages
	{
		[LoggerMessage(EventId = 1, Level = LogLevel.Trace, Message = $"{nameof(TransactionIdProcessor)} added 'transaction.id' tag to Activity.")]
		internal static partial void TransactionIdProcessorTagAdded(this ILogger logger);

		[LoggerMessage(EventId = 100, Level = LogLevel.Warning,
			Message = "Received an unhandled diagnostic event '{EventName}'.")]
		internal static partial void UnhandledDiagnosticEvent(this ILogger logger, string eventName);
	}
}
