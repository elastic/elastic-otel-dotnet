// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using Elastic.OpenTelemetry.Configuration;
using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.Diagnostics.Logging;

internal static class AgentLoggingHelpers
{
	public static LogLevel DefaultLogLevel => LogLevel.Information;

	public static LogLevel GetElasticOtelLogLevelFromEnvironmentVariables()
	{
		var defaultLogLevel = DefaultLogLevel;

		var logLevelEnvironmentVariable = Environment.GetEnvironmentVariable(EnvironmentVariables.ElasticOtelFileLogLevelEnvironmentVariable);

		if (string.IsNullOrEmpty(logLevelEnvironmentVariable))
			return defaultLogLevel;

		var parsedLogLevel = LogLevelHelpers.ToLogLevel(logLevelEnvironmentVariable);
		return parsedLogLevel != LogLevel.None ? parsedLogLevel : defaultLogLevel;
	}

	public static void WriteLogLine(this ILogger logger, Activity? activity, int managedThreadId, DateTime dateTime, LogLevel logLevel, string logLine, string? spanId)
	{
		var state = new LogState
		{
			Activity = activity,
			ManagedThreadId = managedThreadId,
			DateTime = dateTime,
			SpanId = spanId
		};
		logger.Log(logLevel, 0, state, null, (_, _) => logLine);
	}
}
