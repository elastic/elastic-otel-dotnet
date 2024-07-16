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

	public static LogLevel? ToLogLevel(string logLevelString)
	{
		//TRACE does not exist in OTEL_LOG_LEVEL ensure we parse it to next granularity
		//debug, NOTE that OpenTelemetry treats this as invalid and will parse to 'Information'
		//We treat Debug & Trace as a signal global file logging should be enabled.
		if (logLevelString.Equals(Trace, StringComparison.OrdinalIgnoreCase))
			return LogLevel.Debug;
		if (logLevelString.Equals(Debug, StringComparison.OrdinalIgnoreCase))
			return LogLevel.Debug;
		if (logLevelString.Equals("Info", StringComparison.OrdinalIgnoreCase))
			return LogLevel.Information;
		if (logLevelString.Equals(Information, StringComparison.OrdinalIgnoreCase))
			return LogLevel.Information;
		if (logLevelString.Equals("Warn", StringComparison.OrdinalIgnoreCase))
			return LogLevel.Warning;
		if (logLevelString.Equals(Warning, StringComparison.OrdinalIgnoreCase))
			return LogLevel.Warning;
		if (logLevelString.Equals(Error, StringComparison.OrdinalIgnoreCase))
			return LogLevel.Error;
		if (logLevelString.Equals(Critical, StringComparison.OrdinalIgnoreCase))
			return LogLevel.Critical;
		if (logLevelString.Equals(None, StringComparison.OrdinalIgnoreCase))
			return LogLevel.None;
		return null;
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
