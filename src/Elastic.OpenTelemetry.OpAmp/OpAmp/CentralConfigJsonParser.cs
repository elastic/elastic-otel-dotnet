// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

#if !NETFRAMEWORK
using System.Diagnostics.CodeAnalysis;
#endif
using System.Text;

namespace Elastic.OpenTelemetry.OpAmp
{
	/// <summary>
	/// Lightweight JSON parser for extracting configuration values from OpAMP remote config messages.
	/// </summary>
	/// <remarks>
	/// This is a hand-rolled parser to avoid taking a dependency on System.Text.Json, which would
	/// add a transitive dependency for end users and an additional package to manage in the
	/// redistributable.
	/// <para/>
	/// Extracted string values are returned as raw bytes — escape sequences (e.g., <c>\"</c>) are
	/// not decoded. This is acceptable because we control the JSON produced by the Elastic OpAMP
	/// server and configuration values are always plain ASCII strings without escaping.
	/// <para/>
	/// Known limitations (acceptable because the Elastic OpAMP server always emits plain ASCII
	/// property names and simple string values):
	/// <list type="bullet">
	///   <item>Property name matching is literal byte comparison — JSON Unicode escape sequences
	///         in keys (e.g., <c>\u006Cog_level</c> instead of <c>log_level</c>) will not match.</item>
	///   <item>Unicode escape sequences in values are not decoded — they are returned as-is.</item>
	/// </list>
	/// </remarks>
	internal readonly ref struct CentralConfigJsonParser(ReadOnlySpan<byte> json)
	{
		private static ReadOnlySpan<byte> LogLevelPropertyName => "\"log_level\""u8;

		private readonly ReadOnlySpan<byte> _json = json;

#if NETFRAMEWORK
		internal bool TryParseLogLevel(out string? logLevel) =>
			TryParseStringValue(LogLevelPropertyName, out logLevel);
#else
		internal bool TryParseLogLevel([NotNullWhen(true)] out string? logLevel) =>
			TryParseStringValue(LogLevelPropertyName, out logLevel);
#endif

#if NETFRAMEWORK
		private bool TryParseStringValue(ReadOnlySpan<byte> propertyName, out string? value)
#else
		private bool TryParseStringValue(ReadOnlySpan<byte> propertyName, [NotNullWhen(true)] out string? value)
#endif
		{
			value = null;

			var remaining = _json;
			while (true)
			{
				var index = remaining.IndexOf(propertyName);
				if (index == -1)
					return false;

				var pos = index + propertyName.Length;
				remaining = remaining.Slice(pos);

				// Skip whitespace after the property name
				pos = SkipWhitespace(remaining);
				if (pos >= remaining.Length)
					return false;

				// Expect ':'
				if (remaining[pos] != (byte)':')
					continue; // property name appeared as a value, not a key — keep searching

				pos++;
				remaining = remaining.Slice(pos);

				// Skip whitespace after ':'
				pos = SkipWhitespace(remaining);
				if (pos >= remaining.Length)
					return false;

				// Expect opening '"'
				if (remaining[pos] != (byte)'"')
					return false;

				pos++;
				remaining = remaining.Slice(pos);

				// Scan for closing '"', handling escaped characters
				var i = 0;
				while (i < remaining.Length)
				{
					if (remaining[i] == (byte)'\\')
					{
						if (i + 1 >= remaining.Length)
							return false; // truncated escape sequence

						i += 2; // skip escaped character
						continue;
					}

					if (remaining[i] == (byte)'"')
						break;

					i++;
				}

				if (i >= remaining.Length)
					return false;

				var valueBytes = remaining.Slice(0, i);

#if NETFRAMEWORK || NETSTANDARD2_0
				value = Encoding.UTF8.GetString(valueBytes.ToArray());
#else
				value = Encoding.UTF8.GetString(valueBytes);
#endif
				return true;
			}
		}

		private static int SkipWhitespace(ReadOnlySpan<byte> span)
		{
			var i = 0;
			while (i < span.Length)
			{
				var b = span[i];
				if (b != (byte)' ' && b != (byte)'\t' && b != (byte)'\r' && b != (byte)'\n')
					break;
				i++;
			}
			return i;
		}
	}
}
