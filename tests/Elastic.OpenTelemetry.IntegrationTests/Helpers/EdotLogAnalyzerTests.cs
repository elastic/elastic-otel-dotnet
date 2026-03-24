// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.OpenTelemetry.IntegrationTests.Helpers;

public class EdotLogAnalyzerTests : IDisposable
{
	private readonly string _tempDir;

	public EdotLogAnalyzerTests() => _tempDir = Path.Combine(Path.GetTempPath(), $"edot-log-tests-{Guid.NewGuid():N}");

	public void Dispose()
	{
		if (Directory.Exists(_tempDir))
			Directory.Delete(_tempDir, true);
	}

	private string WriteLogFile(params string[] lines)
	{
		Directory.CreateDirectory(_tempDir);
		var path = Path.Combine(_tempDir, "test.log");
		File.WriteAllLines(path, lines);
		return path;
	}

	[Fact]
	public void ParsesStandardLogLine_WithEventIdAndName()
	{
		var path = WriteLogFile(
			"[2026-03-19T10:30:00.0000000Z][000001][------][Debug]  CentralConfiguration: Using isolated load context {{EventId: 102, EventName: UsingIsolatedLoadContext}}");

		var analyzer = new EdotLogAnalyzer(path);

		Assert.Single(analyzer.Entries);
		var entry = analyzer.Entries[0];
		Assert.Equal("000001", entry.ThreadId);
		Assert.Equal("------", entry.SpanId);
		Assert.Equal("Debug", entry.Level);
		Assert.Equal(102, entry.EventId);
		Assert.Equal("UsingIsolatedLoadContext", entry.EventName);
		Assert.Contains("Using isolated load context", entry.Message);
	}

	[Fact]
	public void ParsesLogLine_WithEventIdOnly()
	{
		var path = WriteLogFile(
			"[2026-03-19T10:30:00.0000000Z][000001][------][Information]  Some message {{EventId: 42}}");

		var analyzer = new EdotLogAnalyzer(path);

		Assert.Single(analyzer.Entries);
		Assert.Equal(42, analyzer.Entries[0].EventId);
		Assert.Null(analyzer.Entries[0].EventName);
	}

	[Fact]
	public void ParsesLogLine_WithoutEventId()
	{
		var path = WriteLogFile(
			"[2026-03-19T10:30:00.0000000Z][000001][------][Information]  Elastic Distribution of OpenTelemetry (EDOT) .NET: 1.0.0");

		var analyzer = new EdotLogAnalyzer(path);

		Assert.Single(analyzer.Entries);
		Assert.Null(analyzer.Entries[0].EventId);
		Assert.Null(analyzer.Entries[0].EventName);
	}

	[Fact]
	public void ParsesLogLine_WithTraceContext()
	{
		var path = WriteLogFile(
			"[2026-03-19T10:30:00.0000000Z][000001][abcdef][Debug]  Some message {{EventId: 50, EventName: FoundTag}} <00-abcdef1234567890abcdef1234567890-abcdef1234567890-01>");

		var analyzer = new EdotLogAnalyzer(path);

		Assert.Single(analyzer.Entries);
		Assert.Equal("abcdef", analyzer.Entries[0].SpanId);
		Assert.Equal(50, analyzer.Entries[0].EventId);
		Assert.Equal("FoundTag", analyzer.Entries[0].EventName);
		// Trace context should be stripped from message
		Assert.DoesNotContain("<00-", analyzer.Entries[0].Message);
	}

	[Fact]
	public void ParsesMultipleLines()
	{
		var path = WriteLogFile(
			"[2026-03-19T10:30:00.0000000Z][000001][------][Debug]  First message {{EventId: 1, EventName: BootstrapInvoked}}",
			"[2026-03-19T10:30:01.0000000Z][000001][------][Information]  Second message {{EventId: 101, EventName: InitializingCentralConfig}}",
			"[2026-03-19T10:30:02.0000000Z][000001][------][Error]  Third message {{EventId: 152, EventName: FactoryTypeNotFound}}");

		var analyzer = new EdotLogAnalyzer(path);

		Assert.Equal(3, analyzer.Entries.Count);
		Assert.Equal(1, analyzer.Entries[0].EventId);
		Assert.Equal(101, analyzer.Entries[1].EventId);
		Assert.Equal(152, analyzer.Entries[2].EventId);
	}

	[Fact]
	public void SkipsBlankLines()
	{
		var path = WriteLogFile(
			"[2026-03-19T10:30:00.0000000Z][000001][------][Debug]  Message {{EventId: 1, EventName: BootstrapInvoked}}",
			"",
			"   ",
			"[2026-03-19T10:30:01.0000000Z][000001][------][Debug]  Message {{EventId: 2, EventName: ComponentsCreated}}");

		var analyzer = new EdotLogAnalyzer(path);

		Assert.Equal(2, analyzer.Entries.Count);
	}

	[Fact]
	public void AssertContainsEventId_Passes_WhenPresent()
	{
		var path = WriteLogFile(
			"[2026-03-19T10:30:00.0000000Z][000001][------][Debug]  Message {{EventId: 102, EventName: UsingIsolatedLoadContext}}");

		var analyzer = new EdotLogAnalyzer(path);
		analyzer.AssertContainsEventId(102); // should not throw
	}

	[Fact]
	public void AssertContainsEventId_Fails_WhenAbsent()
	{
		var path = WriteLogFile(
			"[2026-03-19T10:30:00.0000000Z][000001][------][Debug]  Message {{EventId: 102, EventName: UsingIsolatedLoadContext}}");

		var analyzer = new EdotLogAnalyzer(path);
		Assert.ThrowsAny<Exception>(() => analyzer.AssertContainsEventId(999));
	}

	[Fact]
	public void AssertDoesNotContainEventId_Passes_WhenAbsent()
	{
		var path = WriteLogFile(
			"[2026-03-19T10:30:00.0000000Z][000001][------][Debug]  Message {{EventId: 102, EventName: UsingIsolatedLoadContext}}");

		var analyzer = new EdotLogAnalyzer(path);
		analyzer.AssertDoesNotContainEventId(999); // should not throw
	}

	[Fact]
	public void AssertDoesNotContainEventId_Fails_WhenPresent()
	{
		var path = WriteLogFile(
			"[2026-03-19T10:30:00.0000000Z][000001][------][Debug]  Message {{EventId: 102, EventName: UsingIsolatedLoadContext}}");

		var analyzer = new EdotLogAnalyzer(path);
		Assert.ThrowsAny<Exception>(() => analyzer.AssertDoesNotContainEventId(102));
	}

	[Fact]
	public void AssertNoErrors_Passes_WhenNoErrors()
	{
		var path = WriteLogFile(
			"[2026-03-19T10:30:00.0000000Z][000001][------][Debug]  Message {{EventId: 1, EventName: BootstrapInvoked}}",
			"[2026-03-19T10:30:01.0000000Z][000001][------][Warning]  Warning message {{EventId: 8, EventName: MultipleWithElasticDefaultsCalls}}");

		var analyzer = new EdotLogAnalyzer(path);
		analyzer.AssertNoErrors(); // should not throw
	}

	[Fact]
	public void AssertNoErrors_Fails_WhenErrorPresent()
	{
		var path = WriteLogFile(
			"[2026-03-19T10:30:00.0000000Z][000001][------][Error]  Error message {{EventId: 152, EventName: FactoryTypeNotFound}}");

		var analyzer = new EdotLogAnalyzer(path);
		Assert.ThrowsAny<Exception>(() => analyzer.AssertNoErrors());
	}

	[Fact]
	public void AssertNoErrors_AllowsSpecificErrorEventIds()
	{
		var path = WriteLogFile(
			"[2026-03-19T10:30:00.0000000Z][000001][------][Error]  Expected error {{EventId: 152, EventName: FactoryTypeNotFound}}");

		var analyzer = new EdotLogAnalyzer(path);
		analyzer.AssertNoErrors(allowedErrorEventIds: [152]); // should not throw — 152 is allowed
	}

	[Fact]
	public void AssertContainsLevel_Passes_WhenPresent()
	{
		var path = WriteLogFile(
			"[2026-03-19T10:30:00.0000000Z][000001][------][Warning]  Warning message");

		var analyzer = new EdotLogAnalyzer(path);
		analyzer.AssertContainsLevel("Warning"); // should not throw
	}

	[Fact]
	public void AssertContainsLevel_Fails_WhenAbsent()
	{
		var path = WriteLogFile(
			"[2026-03-19T10:30:00.0000000Z][000001][------][Debug]  Debug message");

		var analyzer = new EdotLogAnalyzer(path);
		Assert.ThrowsAny<Exception>(() => analyzer.AssertContainsLevel("Error"));
	}

	[Fact]
	public void AssertNoErrors_Fails_WhenCriticalPresent()
	{
		var path = WriteLogFile(
			"[2026-03-19T10:30:00.0000000Z][000001][------][Critical]  Critical failure");

		var analyzer = new EdotLogAnalyzer(path);
		Assert.ThrowsAny<Exception>(() => analyzer.AssertNoErrors());
	}

	[Fact]
	public void AssertNoErrors_Fails_ForErrorWithoutEventId()
	{
		var path = WriteLogFile(
			"[2026-03-19T10:30:00.0000000Z][000001][------][Error]  Bare error with no EventId");

		var analyzer = new EdotLogAnalyzer(path);
		// Errors without EventId cannot be allowlisted and should always fail
		Assert.ThrowsAny<Exception>(() => analyzer.AssertNoErrors(allowedErrorEventIds: [999]));
	}

	[Fact]
	public void SkipsMalformedLines()
	{
		var path = WriteLogFile(
			"[partial line with missing brackets",
			"not a log line at all",
			"[2026-03-19T10:30:00.0000000Z][000001][------][Debug]  Valid line {{EventId: 1, EventName: BootstrapInvoked}}",
			"[2026-03-19T10:30:00.0000000Z][000001][------]",  // missing level bracket
			"");

		var analyzer = new EdotLogAnalyzer(path);
		// Only the valid line should be parsed
		Assert.Single(analyzer.Entries);
		Assert.Equal(1, analyzer.Entries[0].EventId);
	}

	[Fact]
	public void Timestamp_PreservesUtcKind()
	{
		var path = WriteLogFile(
			"[2026-03-19T10:30:00.0000000Z][000001][------][Debug]  Message {{EventId: 1, EventName: BootstrapInvoked}}");

		var analyzer = new EdotLogAnalyzer(path);
		Assert.Equal(DateTimeKind.Utc, analyzer.Entries[0].Timestamp.Kind);
	}

	[Fact]
	public void ThrowsFileNotFoundException_ForMissingFile() =>
		Assert.Throws<FileNotFoundException>(() => new EdotLogAnalyzer("/nonexistent/path.log"));
}
