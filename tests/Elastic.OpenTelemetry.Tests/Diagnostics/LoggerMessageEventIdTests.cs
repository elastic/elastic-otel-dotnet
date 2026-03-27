// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.Tests.Diagnostics;

/// <summary>
/// Validates that LoggerMessage event IDs are unique across all LoggerMessages classes
/// that are source-linked into the same assembly. Event ID collisions cause ambiguous
/// log queries and should be caught at build time.
/// </summary>
public class LoggerMessageEventIdTests
{
	[Fact]
	public void AllLoggerMessageEventIds_AreUnique()
	{
		// The test assembly compiles against Elastic.OpenTelemetry which source-links
		// Core, OpAmp, and OpAmp.Abstractions. All [LoggerMessage] methods end up in
		// the same assembly and must have unique event IDs.
		var assembly = typeof(Elastic.OpenTelemetry.Diagnostics.CompositeLogger).Assembly;

		var eventIds = assembly
			.GetTypes()
			.SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
			.SelectMany(m => m.GetCustomAttributes<LoggerMessageAttribute>())
			.Select(attr => attr.EventId)
			.Where(id => id != -1) // -1 means auto-generated, skip those
			.ToList();

		var duplicates = eventIds
			.GroupBy(id => id)
			.Where(g => g.Count() > 1)
			.Select(g => g.Key)
			.ToList();

		Assert.True(
			duplicates.Count == 0,
			$"Duplicate LoggerMessage EventIds found: {string.Join(", ", duplicates)}. " +
			$"Each EventId must be unique across all LoggerMessages classes " +
			$"(Core Diagnostics: 1-60, Core Configuration: 100-149, ALC: 150-159, OpAmp: 200+).");
	}

	[Fact]
	public void AllLoggerMessageEventIds_AreUnique_AcrossConditionalCompilation()
	{
		// The reflection-based test above cannot see [LoggerMessage] declarations
		// guarded by #if NET && USE_ISOLATED_OPAMP_CLIENT (they're excluded from
		// the NuGet assembly). This source-scanning test catches collisions across
		// all build configurations.
		var solutionRoot = FindSolutionRoot();
		var srcDir = Path.Combine(solutionRoot, "src");

		var eventIdPattern = new Regex(@"EventId\s*=\s*(\d+)", RegexOptions.Compiled);
		var declarations = new List<(int EventId, string File, int Line)>();

		foreach (var file in Directory.EnumerateFiles(srcDir, "*LoggerMessages*.cs", SearchOption.AllDirectories))
		{
			var relativePath = Path.GetRelativePath(solutionRoot, file);
			var lines = File.ReadAllLines(file);

			for (var i = 0; i < lines.Length; i++)
			{
				var match = eventIdPattern.Match(lines[i]);
				if (match.Success && int.TryParse(match.Groups[1].Value, out var eventId))
					declarations.Add((eventId, relativePath, i + 1));
			}
		}

		var duplicates = declarations
			.GroupBy(d => d.EventId)
			.Where(g => g.Count() > 1)
			.ToList();

		Assert.True(
			duplicates.Count == 0,
			"Duplicate LoggerMessage EventIds found across source files " +
			"(including conditionally-compiled code):\n" +
			string.Join("\n", duplicates.Select(g =>
				$"  EventId {g.Key}: " +
				string.Join(", ", g.Select(d => $"{d.File}:{d.Line}")))));
	}

	/// <summary>
	/// Verifies that every <c>[LoggerMessage]</c> EventId+EventName pair matches a
	/// checked-in snapshot. This guards against accidental renumbering or renaming
	/// that would break log-based assertions in integration tests.
	/// <para>
	/// Set <c>REGENERATE_EVENT_ID_SNAPSHOT=true</c> to overwrite the snapshot file
	/// with the current runtime values instead of asserting.
	/// </para>
	/// </summary>
	[Fact]
	public void AllLoggerMessageEventIds_MatchSnapshot()
	{
		var captured = InvokeAllLoggerMessageMethods();
		var snapshotPath = Path.Combine(FindSolutionRoot(),
			"tests", "Elastic.OpenTelemetry.Tests", "Diagnostics", "event-id-snapshot.json");

		if (string.Equals(Environment.GetEnvironmentVariable("REGENERATE_EVENT_ID_SNAPSHOT"),
				"true", StringComparison.OrdinalIgnoreCase))
		{
			WriteSnapshot(snapshotPath, captured);
			return;
		}

		Assert.True(File.Exists(snapshotPath),
			$"Snapshot file not found at {snapshotPath}. " +
			"Run with REGENERATE_EVENT_ID_SNAPSHOT=true to create it.");

		var expected = ReadSnapshot(snapshotPath);

		// Find entries in snapshot but not in runtime
		var missing = expected
			.Where(e => !captured.Any(c => c.Id == e.Id && c.Name == e.Name))
			.ToList();

		// Find entries in runtime but not in snapshot
		var extra = captured
			.Where(c => !expected.Any(e => e.Id == c.Id && e.Name == c.Name))
			.ToList();

		// Find entries where the EventId exists but the EventName changed
		var renamed = captured
			.Join(expected, c => c.Id, e => e.Id, (c, e) => new { Current = c, Snapshot = e })
			.Where(x => x.Current.Name != x.Snapshot.Name)
			.ToList();

		if (missing.Count == 0 && extra.Count == 0 && renamed.Count == 0)
			return;

		var message = "EventId snapshot mismatch detected. " +
			"Integration tests depend on specific EventIds for log-based assertions.\n" +
			"If this change is intentional, update downstream test assertions and then " +
			"regenerate the snapshot with REGENERATE_EVENT_ID_SNAPSHOT=true.\n\n";

		if (renamed.Count > 0)
			message += "RENAMED (same EventId, different EventName):\n" +
				string.Join("\n", renamed.Select(r =>
					$"  EventId {r.Current.Id}: snapshot has '{r.Snapshot.Name}', runtime has '{r.Current.Name}'")) + "\n\n";

		if (missing.Count > 0)
			message += "IN SNAPSHOT BUT NOT IN RUNTIME (removed or renumbered?):\n" +
				string.Join("\n", missing.Select(m => $"  EventId {m.Id}: {m.Name}")) + "\n\n";

		if (extra.Count > 0)
			message += "IN RUNTIME BUT NOT IN SNAPSHOT (new method? run REGENERATE_EVENT_ID_SNAPSHOT=true):\n" +
				string.Join("\n", extra.Select(e => $"  EventId {e.Id}: {e.Name}")) + "\n\n";

		Assert.Fail(message);
	}

	/// <summary>
	/// Uses reflection to discover and invoke every <c>[LoggerMessage]</c> extension method
	/// in the assembly. This tests the real runtime path — the source-generated code actually
	/// executes — while automatically discovering new methods without manual maintenance.
	/// </summary>
	private static List<(int Id, string? Name)> InvokeAllLoggerMessageMethods()
	{
		var logger = new EventIdCapturingLogger();
		var assembly = typeof(Elastic.OpenTelemetry.Diagnostics.CompositeLogger).Assembly;

		var methods = assembly
			.GetTypes()
			.Where(t => t.IsAbstract && t.IsSealed) // static classes
			.SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
			.Where(m => m.GetCustomAttribute<LoggerMessageAttribute>() is not null)
			.ToList();

		Assert.True(methods.Count > 0,
			"No [LoggerMessage] methods found. This likely indicates a compilation or source-linking issue.");

		foreach (var method in methods)
		{
			var parameters = method.GetParameters();
			var args = new object?[parameters.Length];

			for (var i = 0; i < parameters.Length; i++)
			{
				var paramType = parameters[i].ParameterType;

				if (typeof(ILogger).IsAssignableFrom(paramType))
					args[i] = logger;
				else
					args[i] = CreateDummyValue(paramType);
			}

			method.Invoke(null, args);
		}

		return logger.CapturedEventIds
			.Distinct()
			.OrderBy(e => e.Id)
			.ToList();
	}

	private static object? CreateDummyValue(Type type)
	{
		if (type == typeof(string))
			return string.Empty;
		if (type == typeof(int))
			return 0;
		if (type == typeof(double))
			return 0.0;
		if (type == typeof(bool))
			return false;
		if (type == typeof(Exception))
			return new Exception("test");
		if (type == typeof(StackTrace))
			return new StackTrace();
		if (type == typeof(object))
			return "test";

		// Nullable value types
		if (Nullable.GetUnderlyingType(type) is not null)
			return null;

		// Reference types — return null (covers string? at runtime)
		if (!type.IsValueType)
			return null;

		// Fallback for other value types
		return Activator.CreateInstance(type);
	}

	private sealed record EventIdEntry(int EventId, string? EventName);

	private sealed record EventIdSnapshotRoot(List<EventIdEntry> Snapshot);

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = true
	};

	private static List<(int Id, string? Name)> ReadSnapshot(string path)
	{
		var json = File.ReadAllText(path);
		var root = JsonSerializer.Deserialize<EventIdSnapshotRoot>(json, JsonOptions)
			?? throw new InvalidOperationException($"Failed to deserialize snapshot from {path}");

		return root.Snapshot
			.Select(e => (e.EventId, e.EventName))
			.OrderBy(e => e.EventId)
			.ToList();
	}

	private static void WriteSnapshot(string path, List<(int Id, string? Name)> entries)
	{
		var root = new EventIdSnapshotRoot(
			entries.Select(e => new EventIdEntry(e.Id, e.Name)).ToList());

		var json = JsonSerializer.Serialize(root, JsonOptions);
		File.WriteAllText(path, json + Environment.NewLine);
	}

	private static string FindSolutionRoot()
	{
		var dir = AppContext.BaseDirectory;
		while (dir is not null)
		{
			if (Directory.EnumerateFiles(dir, "*.slnx").Any()
				|| Directory.EnumerateFiles(dir, "*.sln").Any())
				return dir;

			dir = Directory.GetParent(dir)?.FullName;
		}

		throw new InvalidOperationException(
			"Could not find solution root from " + AppContext.BaseDirectory);
	}
}
