// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.Diagnostics.Logging;

internal static class LogLevelHelpers
{
	public const string Critical = "Critical";
	public const string Error = "Error";
	public const string Warning = "Warn";
	public const string Info = "Info";
	public const string Information = "Information";
	public const string Trace = "Trace";
	public const string Debug = "Debug";

	public static LogLevel ToLogLevel(string logLevelString) =>
		logLevelString switch
		{
			Critical => LogLevel.Critical,
			Error => LogLevel.Error,
			Warning => LogLevel.Warning,
			Info => LogLevel.Information,
			Information => LogLevel.Information,
			Debug => LogLevel.Debug,
			Trace => LogLevel.Trace,
			_ => LogLevel.None,
		};

	public static string AsString(this LogLevel logLevel) =>
		logLevel switch
		{
			LogLevel.Critical => Critical,
			LogLevel.Error => Error,
			LogLevel.Warning => Warning,
			LogLevel.Information => Info,
			LogLevel.Debug => Debug,
			LogLevel.Trace => Trace,
			LogLevel.None => string.Empty,
			_ => string.Empty
		};
}
