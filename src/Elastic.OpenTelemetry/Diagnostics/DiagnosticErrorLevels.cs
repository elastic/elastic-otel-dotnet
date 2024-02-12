// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information
namespace Elastic.OpenTelemetry.Diagnostics;

internal static class DiagnosticErrorLevels
{
	public const string Critical = "Critical";
	public const string Error = "Error";
	public const string Warning = "Warn";
	public const string Info = "Info";
	public const string Trace = "Trace";

	public static LogLevel ToLogLevel(string logLevelString) =>
		logLevelString switch
		{
			Critical => LogLevel.Critical,
			Error => LogLevel.Error,
			Warning => LogLevel.Warning,
			Info => LogLevel.Info,
			Trace => LogLevel.Trace,
			_ => LogLevel.Unknown,
		};

	public static string AsString(this LogLevel logLevel) =>
		logLevel switch
		{
			LogLevel.Critical => Critical,
			LogLevel.Error => Error,
			LogLevel.Warning => Warning,
			LogLevel.Info => Info,
			LogLevel.Trace => Trace,
			LogLevel.Unknown => string.Empty,
			_ => string.Empty
		};
}
