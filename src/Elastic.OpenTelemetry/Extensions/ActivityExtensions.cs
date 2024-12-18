// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using System.Linq;
using Elastic.OpenTelemetry.Processors;

namespace Elastic.OpenTelemetry.Extensions;

internal static class ActivityExtensions
{
	public static bool TryCompress(this Activity buffered, Activity sibling)
	{
		Composite? composite = null;

		var property = buffered.GetCustomProperty("Composite");

		if (property is Composite c)
			composite = c;

		var isAlreadyComposite = composite is not null;

		var canBeCompressed = isAlreadyComposite
			? buffered.TryToCompressComposite(sibling, composite!)
			: buffered.TryToCompressRegular(sibling, ref composite);

		if (!canBeCompressed)
			return false;

		if (!isAlreadyComposite)
		{
			composite ??= new Composite();
			composite.Count = 1;
			composite.DurationSum = buffered.Duration.Milliseconds;
		}

		composite!.Count++;
		composite.DurationSum += sibling.Duration.Milliseconds;

		buffered.SetCustomProperty("Composite", composite);

		var endTime = sibling.StartTimeUtc.Add(sibling.Duration);
		buffered.SetEndTime(endTime);

		sibling.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;

		return true;
	}

	private static bool TryToCompressRegular(this Activity buffered, Activity sibling, ref Composite? composite)
	{
		if (!buffered.IsSameKind(sibling, out var compositeSpanName))
			return false;

		// TODO for exact match, we should also compare events and baggage

		if (buffered.OperationName == sibling.OperationName && buffered.TagsEqual(sibling))
		{
			composite ??= new Composite();
			composite.CompressionStrategy = "exact_match";
			return true;
		}

		// TODO for same kind, should we compare events and baggage?

		composite ??= new Composite();
		composite.CompressionStrategy = "same_kind";
		composite.ActivityName = compositeSpanName;

		return true;
	}

	private static bool TryToCompressComposite(this Activity buffered, Activity sibling, Composite composite)
	{
		if (!buffered.IsSameKind(sibling, out _))
			return false;

		return composite.CompressionStrategy switch
		{
			"exact_match" => buffered.OperationName == sibling.OperationName
				&& buffered.TagsEqual(sibling)
				&& sibling.Duration <= SpanCompressionProcessor.MaxSpanCompressionExactMatchMaxDuration,

			// TODO - For same kind, we should check that all matching tags have the same value and combine with any distinct tags

			"same_kind" => sibling.Duration <= SpanCompressionProcessor.MaxSpanCompressionSameKindMaxDuration,

			_ => false,
		};
	}

	private static bool TagsEqual(this Activity current, Activity other)
	{
		var currentTags = current.TagObjects.ToDictionary(t => t);
		var otherTags = other.TagObjects.ToDictionary(t => t);

		if (currentTags.Count != otherTags.Count)
			return false;

		foreach (var tag in currentTags)
		{
			if (otherTags.TryGetValue(tag.Key, out var otherTag))
				return false;

			if (!tag.Equals(otherTag))
				return false;
		}

		return true;
	}

	private static bool IsSameKind(this Activity current, Activity other, out string compositeSpanName)
	{
		compositeSpanName = "Compressed calls";

		if (current.Kind != other.Kind)
			return false;

		string? currentDbSystem = null;
		string? otherDbSystem = null;
		string? currentDbCollectionName = null;
		string? otherDbCollectionName = null;
		string? currentDbServerAddress = null;
		string? otherDbServerAddress = null;

		// The most efficient comparison we can achieve here involves one full
		// iteration of the tag objects per Activity. We store the values we
		// may later compare during each iteration.

		// TODO - Other comparisons

		foreach (var tag in current.TagObjects)
		{
			if (tag.Key.Equals("db.system", StringComparison.Ordinal))
			{
				currentDbSystem = tag.Value as string;
				continue;
			}

			if (tag.Key.Equals("db.collection.name", StringComparison.Ordinal))
			{
				currentDbCollectionName = tag.Value as string;
				continue;
			}

			if (tag.Key.Equals("server.address", StringComparison.Ordinal))
			{
				currentDbServerAddress = tag.Value as string;
				continue;
			}
		}

		foreach (var tag in other.TagObjects)
		{
			if (tag.Key.Equals("db.system", StringComparison.Ordinal))
			{
				otherDbSystem = tag.Value as string;
				continue;
			}

			if (tag.Key.Equals("db.collection.name", StringComparison.Ordinal))
			{
				otherDbCollectionName = tag.Value as string;
				continue;
			}

			if (tag.Key.Equals("server.address", StringComparison.Ordinal))
			{
				otherDbServerAddress = tag.Value as string;
				continue;
			}
		}

		if (!CompareStringEquality(currentDbSystem, otherDbSystem))
			return false;

		if (!CompareStringEquality(currentDbCollectionName, otherDbCollectionName))
			return false;

		if (!CompareStringEquality(currentDbServerAddress, otherDbServerAddress))
			return false;

		return true;

		static bool CompareStringEquality(string? current, string? other)
		{
			if (current is null && other is null)
				return true;

			if (current is null && other is not null)
				return false;

			if (current is not null && other is null)
				return false;

			if (!current!.Equals(other!, StringComparison.OrdinalIgnoreCase))
				return false;

			return true;
		}
	}
}
