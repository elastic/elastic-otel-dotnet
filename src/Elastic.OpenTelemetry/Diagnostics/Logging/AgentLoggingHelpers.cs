// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.Diagnostics.Logging;

internal record LogState
{
	public Activity? Activity { get; init; }
	public int ManagedThreadId { get; init; }
	public DateTime DateTime { get; init; }
	public string? SpanId { get; init; }
}

internal static class AgentLoggingHelpers
{
	public static bool IsFileLoggingEnabled()
	{
		var logDir = Environment.GetEnvironmentVariable(EnvironmentVariables.ElasticOtelLogDirectoryEnvironmentVariable);
		return !string.IsNullOrWhiteSpace(logDir);
	}

	public static LogLevel GetElasticOtelLogLevel()
	{
		var logLevel = LogLevel.Information;

		var logLevelEnvironmentVariable = Environment.GetEnvironmentVariable(EnvironmentVariables.ElasticOtelLogLevelEnvironmentVariable);

		if (!string.IsNullOrEmpty(logLevelEnvironmentVariable))
		{
			if (logLevelEnvironmentVariable.Equals(LogLevelHelpers.Trace, StringComparison.OrdinalIgnoreCase))
				logLevel = LogLevel.Trace;

			else if (logLevelEnvironmentVariable.Equals(LogLevelHelpers.Debug, StringComparison.OrdinalIgnoreCase))
				logLevel = LogLevel.Debug;

			else if (logLevelEnvironmentVariable.Equals(LogLevelHelpers.Info, StringComparison.OrdinalIgnoreCase))
				logLevel = LogLevel.Information;

			else if (logLevelEnvironmentVariable.Equals(LogLevelHelpers.Information, StringComparison.OrdinalIgnoreCase))
				logLevel = LogLevel.Information;

			else if (logLevelEnvironmentVariable.Equals(LogLevelHelpers.Warning, StringComparison.OrdinalIgnoreCase))
				logLevel = LogLevel.Warning;

			else if (logLevelEnvironmentVariable.Equals(LogLevelHelpers.Error, StringComparison.OrdinalIgnoreCase))
				logLevel = LogLevel.Error;

			else if (logLevelEnvironmentVariable.Equals(LogLevelHelpers.Critical, StringComparison.OrdinalIgnoreCase))
				logLevel = LogLevel.Critical;
		}

		return logLevel;
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
