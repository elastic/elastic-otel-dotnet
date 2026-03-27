// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Globalization;
using System.Text.RegularExpressions;

namespace Elastic.OpenTelemetry.IntegrationTests.Helpers;

/// <summary>
/// Parsed representation of a single EDOT log file line.
/// </summary>
/// <remarks>
/// Format produced by <c>LogFormatter.Format</c>:
/// <code>[ISO8601][ThreadId][SpanId][Level]  message {{EventId: id, EventName: name}} &lt;traceContext&gt;</code>
/// </remarks>
internal sealed record EdotLogEntry(
	DateTime Timestamp,
	string ThreadId,
	string SpanId,
	string Level,
	string Message,
	int? EventId,
	string? EventName);

/// <summary>
/// Parses an EDOT log file and provides assertion helpers for integration tests.
/// Assertions are based on <see cref="Microsoft.Extensions.Logging.EventId"/> values
/// emitted by <c>[LoggerMessage]</c> methods — a stable contract protected by the
/// EventId snapshot test in <c>LoggerMessageEventIdTests</c>.
/// </summary>
/// <remarks>
/// <para><b>LogLevel caveat:</b> <c>LogFormatter</c> forces <c>LogLevel.Error</c> whenever
/// an <see cref="Exception"/> is present, regardless of the declared log level. A
/// <c>[LoggerMessage(Level = LogLevel.Warning)]</c> method that includes an
/// <c>Exception</c> parameter will appear as <c>[Error]</c> in the file. Keep this in
/// mind when using <see cref="AssertContainsLevel"/> or <see cref="AssertNoErrors"/>.</para>
/// </remarks>
internal sealed class EdotLogAnalyzer
{
	// Matches the structured prefix: [timestamp][threadId][spanId][level]
	private static readonly Regex PrefixPattern = new(
		@"^\[(?<timestamp>[^\]]+)\]\[(?<threadId>[^\]]+)\]\[(?<spanId>[^\]]+)\]\[(?<level>[^\]]+)\]",
		RegexOptions.Compiled);

	// Matches the EventId suffix: {{EventId: 102, EventName: UsingIsolatedLoadContext}}
	// EventName is optional (LogFormatter omits it when EventId.Name is null/empty).
	private static readonly Regex EventIdPattern = new(
		@"\{\{EventId:\s*(?<eventId>\d+)(?:,\s*EventName:\s*(?<eventName>\w+))?\}\}",
		RegexOptions.Compiled);

	public EdotLogAnalyzer(string logFilePath)
	{
		if (!File.Exists(logFilePath))
			throw new FileNotFoundException($"EDOT log file not found: {logFilePath}", logFilePath);

		var lines = File.ReadAllLines(logFilePath);
		var entries = new List<EdotLogEntry>(lines.Length);

		foreach (var line in lines)
		{
			if (string.IsNullOrWhiteSpace(line))
				continue;

			var entry = ParseLine(line);
			if (entry is not null)
				entries.Add(entry);
		}

		Entries = entries;
	}

	/// <summary>All parsed log entries.</summary>
	public IReadOnlyList<EdotLogEntry> Entries { get; }

	/// <summary>Assert that a specific EventId appears at least once.</summary>
	public void AssertContainsEventId(int eventId, string because = "")
	{
		var found = Entries.Any(e => e.EventId == eventId);
		var reason = string.IsNullOrEmpty(because) ? "" : $" because {because}";
		Assert.True(found,
			$"Expected log to contain EventId {eventId}{reason}, " +
			$"but it was not found among {Entries.Count} entries. " +
			$"EventIds present: [{string.Join(", ", Entries.Where(e => e.EventId.HasValue).Select(e => e.EventId).Distinct().OrderBy(id => id))}]");
	}

	/// <summary>Assert that a specific EventId does NOT appear.</summary>
	public void AssertDoesNotContainEventId(int eventId, string because = "")
	{
		var matching = Entries.Where(e => e.EventId == eventId).ToList();
		if (matching.Count == 0)
			return;

		var reason = string.IsNullOrEmpty(because) ? "" : $" because {because}";
		Assert.Fail(
			$"Expected log to NOT contain EventId {eventId}{reason}, " +
			$"but found {matching.Count} occurrence(s). " +
			$"First match: \"{matching[0].Message}\"");
	}

	/// <summary>
	/// Assert no entries at Error or Critical level, except for explicitly allowed EventIds.
	/// Errors without an EDOT EventId (e.g., from upstream libraries) can be allowed
	/// by passing substrings to <paramref name="allowedMessageSubstrings"/>.
	/// </summary>
	public void AssertNoErrors(int[]? allowedErrorEventIds = null, string[]? allowedMessageSubstrings = null)
	{
		allowedErrorEventIds ??= [];
		allowedMessageSubstrings ??= [];

		var errors = Entries
			.Where(e => e.Level is "Error" or "Critical")
			.Where(e => !e.EventId.HasValue || !allowedErrorEventIds.Contains(e.EventId.Value))
			.Where(e => !allowedMessageSubstrings.Any(sub => e.Message.Contains(sub, StringComparison.OrdinalIgnoreCase)))
			.ToList();

		Assert.True(errors.Count == 0,
			$"Expected no Error/Critical log entries (allowed EventIds: [{string.Join(", ", allowedErrorEventIds)}]), " +
			$"but found {errors.Count}:\n" +
			string.Join("\n", errors.Select(e =>
				$"  [{e.Level}] EventId={e.EventId}: {e.Message}")));
	}

	/// <summary>Assert that a specific log level appears at least once.</summary>
	public void AssertContainsLevel(string level)
	{
		var found = Entries.Any(e => e.Level.Equals(level, StringComparison.OrdinalIgnoreCase));
		Assert.True(found,
			$"Expected log to contain at least one '{level}' entry, " +
			$"but levels present were: [{string.Join(", ", Entries.Select(e => e.Level).Distinct())}]");
	}

	private static EdotLogEntry? ParseLine(string line)
	{
		var prefixMatch = PrefixPattern.Match(line);
		if (!prefixMatch.Success)
			return null;

		var timestamp = DateTime.TryParseExact(
				prefixMatch.Groups["timestamp"].Value, "O", CultureInfo.InvariantCulture,
				DateTimeStyles.RoundtripKind, out var ts)
			? ts
			: DateTime.MinValue;
		var threadId = prefixMatch.Groups["threadId"].Value;
		var spanId = prefixMatch.Groups["spanId"].Value;
		var level = prefixMatch.Groups["level"].Value;

		// Everything after the prefix (skip padding whitespace)
		var rest = line[prefixMatch.Length..].TrimStart();

		// Extract EventId from the rest, then treat everything before it as the message
		int? eventId = null;
		string? eventName = null;
		var message = rest;

		var eventIdMatch = EventIdPattern.Match(rest);
		if (eventIdMatch.Success)
		{
			eventId = int.Parse(eventIdMatch.Groups["eventId"].Value);
			eventName = eventIdMatch.Groups["eventName"].Success
				? eventIdMatch.Groups["eventName"].Value
				: null;

			// Message is everything before the {{EventId:...}} block
			message = rest[..eventIdMatch.Index].TrimEnd();
		}

		// Strip trailing trace context <00-...> if present after removing EventId
		var traceContextIndex = message.LastIndexOf(" <00-", StringComparison.Ordinal);
		if (traceContextIndex >= 0)
			message = message[..traceContextIndex];

		return new EdotLogEntry(timestamp, threadId, spanId, level, message, eventId, eventName);
	}
}
