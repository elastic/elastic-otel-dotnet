// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.Diagnostics.Logging;

internal static class LogLevelHelpers
{
	public const string Critical = "Critical";
	public const string Error = "Error";
	public const string Warning = "Warning";
	public const string Information = "Information";
	public const string Debug = "Debug";
	public const string Trace = "Trace";
	public const string None = "None";

	public static LogLevel ToLogLevel(string logLevelString)
	{
		var logLevel = LogLevel.None;

		if (logLevelString.Equals(Trace, StringComparison.OrdinalIgnoreCase))
			logLevel = LogLevel.Trace;

		else if (logLevelString.Equals(Debug, StringComparison.OrdinalIgnoreCase))
			logLevel = LogLevel.Debug;

		else if (logLevelString.Equals(Information, StringComparison.OrdinalIgnoreCase))
			logLevel = LogLevel.Information;

		else if (logLevelString.Equals(Information, StringComparison.OrdinalIgnoreCase))
			logLevel = LogLevel.Information;

		else if (logLevelString.Equals(Warning, StringComparison.OrdinalIgnoreCase))
			logLevel = LogLevel.Warning;

		else if (logLevelString.Equals(Error, StringComparison.OrdinalIgnoreCase))
			logLevel = LogLevel.Error;

		else if (logLevelString.Equals(Critical, StringComparison.OrdinalIgnoreCase))
			logLevel = LogLevel.Critical;

		return logLevel;
	}

	public static string AsString(this LogLevel logLevel) =>
		logLevel switch
		{
			LogLevel.Critical => Critical,
			LogLevel.Error => Error,
			LogLevel.Warning => Warning,
			LogLevel.Information => Information,
			LogLevel.Debug => Debug,
			LogLevel.Trace => Trace,
			LogLevel.None => None,
			_ => string.Empty
		};
}
