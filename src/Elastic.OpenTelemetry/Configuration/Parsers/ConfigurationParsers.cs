// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Diagnostics.Logging;
using Microsoft.Extensions.Logging;
using static System.StringComparison;
using static System.StringSplitOptions;

namespace Elastic.OpenTelemetry.Configuration.Parsers;

internal static class ConfigurationParsers {

	internal static (bool, LogLevel?) LogLevelParser(string? s) =>
		!string.IsNullOrEmpty(s) ? (true, LogLevelHelpers.ToLogLevel(s)) : (false, null);

	internal static (bool, LogTargets?) LogTargetsParser(string? s)
	{
		//var tokens = s?.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries });
		if (string.IsNullOrWhiteSpace(s))
			return (false, null);

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
		return !found ? (false, null) : (true, logTargets);

		bool IsSet(string k, string v)
		{
			var b = k.Trim().Equals(v, InvariantCultureIgnoreCase);
			if (b)
				found = true;
			return b;
		}
	}

	internal static (bool, ElasticDefaults?) EnabledDefaultsParser(string? s)
	{
		if (string.IsNullOrWhiteSpace(s))
			return (false, null);

		var enabledDefaults = ElasticDefaults.None;
		var found = false;

		foreach (var target in s.Split(new[] { ';', ',' }, RemoveEmptyEntries))
		{
			if (IsSet(target, nameof(ElasticDefaults.Tracing)))
				enabledDefaults |= ElasticDefaults.Tracing;
			else if (IsSet(target, nameof(ElasticDefaults.Metrics)))
				enabledDefaults |= ElasticDefaults.Metrics;
			else if (IsSet(target, nameof(ElasticDefaults.Logging)))
				enabledDefaults |= ElasticDefaults.Logging;
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
		return !found ? (false, null) : (true, enabledDefaults);

		bool IsSet(string k, string v)
		{
			var b = k.Trim().Equals(v, InvariantCultureIgnoreCase);
			if (b)
				found = true;
			return b;
		}
	}

	internal static (bool, Signals?) EnabledSignalsParser(string? s)
	{
		if (string.IsNullOrWhiteSpace(s))
			return (false, null);

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
		return !found ? (false, null) : (true, enabledDefaults);

		bool IsSet(string k, string v)
		{
			var b = k.Trim().Equals(v, InvariantCultureIgnoreCase);
			if (b)
				found = true;
			return b;
		}
	}

	internal static (bool, string) StringParser(string? s) => !string.IsNullOrEmpty(s) ? (true, s) : (false, string.Empty);

	internal static (bool, bool?) BoolParser(string? s) =>
		s switch
		{
			"1" => (true, true),
			"0" => (true, false),
			_ => bool.TryParse(s, out var boolValue) ? (true, boolValue) : (false, null)
		};
}
