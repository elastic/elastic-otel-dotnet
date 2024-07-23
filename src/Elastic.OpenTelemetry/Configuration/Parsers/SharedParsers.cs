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
		//var tokens = s?.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries });
		if (string.IsNullOrWhiteSpace(s))
			return null;

		var logTargets = LogTargets.None;
		var found = false;

		foreach (var target in s.Split(new[] { ';', ',' }, RemoveEmptyEntries))
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

	internal static ElasticDefaults? ElasticDefaultsParser(string? s)
	{
		if (string.IsNullOrWhiteSpace(s))
			return null;

		var enabledDefaults = ElasticDefaults.None;
		var found = false;

		foreach (var target in s.Split(new[] { ';', ',' }, RemoveEmptyEntries))
		{
			if (IsSet(target, nameof(ElasticDefaults.Traces)))
				enabledDefaults |= ElasticDefaults.Traces;
			else if (IsSet(target, nameof(ElasticDefaults.Metrics)))
				enabledDefaults |= ElasticDefaults.Metrics;
			else if (IsSet(target, nameof(ElasticDefaults.Logs)))
				enabledDefaults |= ElasticDefaults.Logs;
			else if (IsSet(target, nameof(ElasticDefaults.All)))
			{
				enabledDefaults = ElasticDefaults.All;
				break;
			}
			else if (IsSet(target, "none"))
			{
				enabledDefaults = ElasticDefaults.None;
				break;
			}
		}
		return !found ? null : enabledDefaults;

		bool IsSet(string k, string v)
		{
			var b = k.Trim().Equals(v, InvariantCultureIgnoreCase);
			if (b)
				found = true;
			return b;
		}
	}

	internal static Signals? SignalsParser(string? s)
	{
		if (string.IsNullOrWhiteSpace(s))
			return null;

		var enabledDefaults = Signals.None;
		var found = false;

		foreach (var target in s.Split(new[] { ';', ',' }, RemoveEmptyEntries))
		{
			if (IsSet(target, nameof(Signals.Traces)))
				enabledDefaults |= Signals.Traces;
			else if (IsSet(target, nameof(Signals.Metrics)))
				enabledDefaults |= Signals.Metrics;
			else if (IsSet(target, nameof(Signals.Logs)))
				enabledDefaults |= Signals.Logs;
			else if (IsSet(target, nameof(Signals.All)))
			{
				enabledDefaults = Signals.All;
				break;
			}
			else if (IsSet(target, "none"))
			{
				enabledDefaults = Signals.None;
				break;
			}
		}
		return !found ? null : enabledDefaults;

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
