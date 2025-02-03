// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Diagnostics.Logging;
using Microsoft.Extensions.Logging;
using static System.StringComparison;
using static System.StringSplitOptions;

namespace Elastic.OpenTelemetry.Configuration.Parsers;

internal static class SharedParsers
{
	internal static LogLevel? LogLevelParser(string? s) =>
		!string.IsNullOrEmpty(s) ? LogLevelHelpers.ToLogLevel(s) : null;

	internal static LogTargets? LogTargetsParser(string? s)
	{
		if (string.IsNullOrWhiteSpace(s))
			return null;

		var logTargets = LogTargets.None;
		var found = false;

		foreach (var target in s.Split([';', ','], RemoveEmptyEntries))
		{
			if (IsSet(target, "stdout"))
				logTargets |= LogTargets.StdOut;
			else if (IsSet(target, "file"))
				logTargets |= LogTargets.File;
			else if (IsSet(target, "none"))
				logTargets |= LogTargets.None;
		}
		return !found ? null : logTargets;

		bool IsSet(string k, string v)
		{
			var b = k.Trim().Equals(v, InvariantCultureIgnoreCase);
			if (b)
				found = true;
			return b;
		}
	}

	internal static string? StringParser(string? s) => !string.IsNullOrEmpty(s) ? s : null;

	internal static bool? BoolParser(string? s) =>
		s switch
		{
			"1" => true,
			"0" => false,
			_ => bool.TryParse(s, out var boolValue) ? boolValue : null
		};
}
