// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Text;
using Elastic.OpenTelemetry.OpAmp;

namespace Elastic.OpenTelemetry.Tests;

public class CentralConfigJsonParserTests
{
	[Fact]
	public void Minified_ParsesLogLevel()
	{
		var json = "{\"log_level\":\"info\"}"u8;
		var parser = new CentralConfigJsonParser(json);
		Assert.True(parser.TryParseLogLevel(out var logLevel));
		Assert.Equal("info", logLevel);
	}

	[Fact]
	public void SpaceAfterColon_ParsesLogLevel()
	{
		var json = "{\"log_level\": \"info\"}"u8;
		var parser = new CentralConfigJsonParser(json);
		Assert.True(parser.TryParseLogLevel(out var logLevel));
		Assert.Equal("info", logLevel);
	}

	[Fact]
	public void SpacesAroundColon_ParsesLogLevel()
	{
		var json = "{\"log_level\" : \"info\"}"u8;
		var parser = new CentralConfigJsonParser(json);
		Assert.True(parser.TryParseLogLevel(out var logLevel));
		Assert.Equal("info", logLevel);
	}

	[Fact]
	public void PrettyPrinted_ParsesLogLevel()
	{
		var json = Encoding.UTF8.GetBytes("""
			{
				"log_level": "warn"
			}
			""");
		var parser = new CentralConfigJsonParser(json);
		Assert.True(parser.TryParseLogLevel(out var logLevel));
		Assert.Equal("warn", logLevel);
	}

	[Fact]
	public void ReorderedProperties_ParsesLogLevel()
	{
		var json = "{\"other\":\"value\",\"log_level\":\"debug\"}"u8;
		var parser = new CentralConfigJsonParser(json);
		Assert.True(parser.TryParseLogLevel(out var logLevel));
		Assert.Equal("debug", logLevel);
	}

	[Fact]
	public void LogLevelAsValueBeforeKey_ParsesCorrectLogLevel()
	{
		var json = "{\"desc\":\"log_level\",\"log_level\":\"warn\"}"u8;
		var parser = new CentralConfigJsonParser(json);
		Assert.True(parser.TryParseLogLevel(out var logLevel));
		Assert.Equal("warn", logLevel);
	}

	[Fact]
	public void MissingLogLevel_ReturnsFalse()
	{
		var json = "{\"other\":\"value\"}"u8;
		var parser = new CentralConfigJsonParser(json);
		Assert.False(parser.TryParseLogLevel(out var logLevel));
		Assert.Null(logLevel);
	}

	[Fact]
	public void EmptyObject_ReturnsFalse()
	{
		var json = "{}"u8;
		var parser = new CentralConfigJsonParser(json);
		Assert.False(parser.TryParseLogLevel(out var logLevel));
		Assert.Null(logLevel);
	}

	[Theory]
	[InlineData("trace")]
	[InlineData("debug")]
	[InlineData("info")]
	[InlineData("warn")]
	[InlineData("error")]
	[InlineData("fatal")]
	public void VariousLogLevels_ParseCorrectly(string level)
	{
		var json = Encoding.UTF8.GetBytes($"{{\"log_level\":\"{level}\"}}");
		var parser = new CentralConfigJsonParser(json);
		Assert.True(parser.TryParseLogLevel(out var logLevel));
		Assert.Equal(level, logLevel);
	}

	[Fact]
	public void EscapedQuoteInPrecedingProperty_ParsesLogLevel()
	{
		var json = "{\"desc\":\"has \\\"quotes\\\"\",\"log_level\":\"error\"}"u8;
		var parser = new CentralConfigJsonParser(json);
		Assert.True(parser.TryParseLogLevel(out var logLevel));
		Assert.Equal("error", logLevel);
	}

	[Fact]
	public void TruncatedMidValue_ReturnsFalse()
	{
		var json = "{\"log_level\":\"inf"u8;
		var parser = new CentralConfigJsonParser(json);
		Assert.False(parser.TryParseLogLevel(out var logLevel));
		Assert.Null(logLevel);
	}

	[Fact]
	public void TrailingBackslashInValue_ReturnsFalse()
	{
		var json = "{\"log_level\":\"info\\"u8;
		var parser = new CentralConfigJsonParser(json);
		Assert.False(parser.TryParseLogLevel(out var logLevel));
		Assert.Null(logLevel);
	}

	[Fact]
	public void EmptyStringValue_ReturnsTrue()
	{
		var json = "{\"log_level\":\"\"}"u8;
		var parser = new CentralConfigJsonParser(json);
		Assert.True(parser.TryParseLogLevel(out var logLevel));
		Assert.Equal("", logLevel);
	}

	[Fact]
	public void EscapedQuoteInsideValue_ReturnsRawBytes()
	{
		// The parser does not unescape — it returns the raw bytes between the opening and
		// closing quotes. This is acceptable because we control the JSON produced by the
		// Elastic OpAMP server and values never contain escape sequences in practice.
		var json = "{\"log_level\":\"in\\\"fo\"}"u8;
		var parser = new CentralConfigJsonParser(json);
		Assert.True(parser.TryParseLogLevel(out var logLevel));
		Assert.Equal(6, logLevel?.Length);
		Assert.Equal("in\\\"fo", logLevel);
	}

	[Fact]
	public void EmptySpan_ReturnsFalse()
	{
		var parser = new CentralConfigJsonParser(ReadOnlySpan<byte>.Empty);
		Assert.False(parser.TryParseLogLevel(out var logLevel));
		Assert.Null(logLevel);
	}

	[Fact]
	public void NonStringValueNumber_ReturnsFalse()
	{
		var json = "{\"log_level\":123}"u8;
		var parser = new CentralConfigJsonParser(json);
		Assert.False(parser.TryParseLogLevel(out var logLevel));
		Assert.Null(logLevel);
	}

	[Fact]
	public void NonStringValueNull_ReturnsFalse()
	{
		var json = "{\"log_level\":null}"u8;
		var parser = new CentralConfigJsonParser(json);
		Assert.False(parser.TryParseLogLevel(out var logLevel));
		Assert.Null(logLevel);
	}

	[Fact]
	public void TruncatedAfterOpeningQuote_ReturnsFalse()
	{
		var json = "{\"log_level\":\""u8;
		var parser = new CentralConfigJsonParser(json);
		Assert.False(parser.TryParseLogLevel(out var logLevel));
		Assert.Null(logLevel);
	}

	[Fact]
	public void NonStringValueArray_ReturnsFalse()
	{
		var json = "{\"log_level\":[]}"u8;
		var parser = new CentralConfigJsonParser(json);
		Assert.False(parser.TryParseLogLevel(out var logLevel));
		Assert.Null(logLevel);
	}

	[Fact]
	public void NonStringValueObject_ReturnsFalse()
	{
		var json = "{\"log_level\":{}}"u8;
		var parser = new CentralConfigJsonParser(json);
		Assert.False(parser.TryParseLogLevel(out var logLevel));
		Assert.Null(logLevel);
	}

	[Fact]
	public void NestedObjects_ParsesTopLevelLogLevel()
	{
		var json = "{\"meta\":{\"version\":1},\"log_level\":\"warn\"}"u8;
		var parser = new CentralConfigJsonParser(json);
		Assert.True(parser.TryParseLogLevel(out var logLevel));
		Assert.Equal("warn", logLevel);
	}

	[Fact]
	public void Utf8BomPrefix_StillParsesLogLevel()
	{
		// A UTF-8 BOM (0xEF 0xBB 0xBF) prepended to the JSON is harmless because
		// the parser uses Span.IndexOf — a substring search that finds "log_level"
		// regardless of leading bytes. Unlikely for a protobuf bytes field, but safe
		// if it ever happens.
		var bom = new byte[] { 0xEF, 0xBB, 0xBF };
		var body = "{\"log_level\":\"info\"}"u8;
		var withBom = new byte[bom.Length + body.Length];
		bom.CopyTo(withBom, 0);
		body.CopyTo(withBom.AsSpan(bom.Length));

		var parser = new CentralConfigJsonParser(withBom);
		Assert.True(parser.TryParseLogLevel(out var logLevel));
		Assert.Equal("info", logLevel);
	}

	[Fact]
	public void WhitespaceOnlyInput_ReturnsFalse()
	{
		var json = "   "u8;
		var parser = new CentralConfigJsonParser(json);
		Assert.False(parser.TryParseLogLevel(out var logLevel));
		Assert.Null(logLevel);
	}
}
