// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Elastic.OpenTelemetry.Extensions;
using Microsoft.Extensions.Logging;
using OpenTelemetry;

namespace Elastic.OpenTelemetry.Processors;

/// <summary>
/// A processor that can mark spans as compressed/composite.
/// </summary>
internal sealed class SpanCompressionProcessor(ILogger logger) : BaseProcessor<Activity>
{
	private readonly ILogger _logger = logger;

	// IMPLEMENTATION NOTES:

	// When each new Activity (span) is started, we pre-determine if it may be eligable for span compression.
	// Parent spans or spans which have child spans are excluded from being compressed. Additionally,
	// certain known spans, such as those representing outbound HTTP are not eligable since they may
	// propogate tracecontext and therefore, any compression may orphan spans from continued distributed traces.
	// A hashset of span IDs is used to track those spans which may be eligable. This allows us to use the
	// creation of a child span as a trigger to remove the eligability of its parent from compression.

	// OVERHEAD NOTES:

	// When enabled (by default) any potentially compressible spans will be buffered. We buffer up one child, per
	// parent activity. This incurs a small memory overhead for each currently running parent. Further, for each
	// Activity we buffer, we must later flush it, which requires us to create a copy of that Activity, in order,
	// for it to be recorded. This therefore doubles the Activity allocation overhead (per eligable child). This
	// is limited to those Activities which are eligable at the time they are stopped which is generally expected
	// to be a relatively small subset of the overall activities in the system using the default durations.

	// TODO - Consider emitting "meta" metrics for the current buffer sizes.

	private const int SpanCompressionExactMatchMaxDurationMs = 50;
	private const int SpanCompressionSameKindMaxDurationMs = 50; // TODO; Set to 0 by default, per existing agent

	// TODO: 1024 is arbitrarily chosen here during prototyping.
	private const int MaxPotentialCompressedSpansBufferSize = 1024;

	internal static readonly TimeSpan MaxSpanCompressionExactMatchMaxDuration = TimeSpan.FromMilliseconds(SpanCompressionExactMatchMaxDurationMs);
	internal static readonly TimeSpan MaxSpanCompressionSameKindMaxDuration = TimeSpan.FromMilliseconds(SpanCompressionSameKindMaxDurationMs);

	private static readonly TimeSpan MaxSpanCompressionDuration = TimeSpan.FromMilliseconds(Math.Max(SpanCompressionExactMatchMaxDurationMs, SpanCompressionSameKindMaxDurationMs));

	// NOTE: Safe to use a non-current collection for this use case
	private readonly HashSet<ActivitySpanId> _potentialCompressedSpans = [];

#pragma warning disable IDE0028
	// Stores the last seen child for a span, which may then be compressed with the next sibling.
	private readonly ConditionalWeakTable<Activity, Activity> _compressionBuffer = new();
#pragma warning restore IDE0028

	/// <inheritdoc cref="OnStart"/>
	public override void OnStart(Activity activity)
	{
		try
		{
			// We don't attempt to compress parent spans
			if (activity.Parent is null)
			{
				base.OnStart(activity);
				return;
			}

			// If we see a child span for any in-flight potential compressed spans, we can remove them as they
			// are no longer eligable.
			if (_potentialCompressedSpans.Remove(activity.ParentSpanId))
			{
				_logger.LogTrace("Removed {SpanId} from compression eligable as it has a child.", activity.ParentSpanId);
			}

			// This is used to prevent replacement spans (created during flushing) from being considered for compression.
			// It may also be documented as a mechanism for consumers to mark spans as ineligable.
			var skipCompressionProperty = activity.GetCustomProperty("SkipCompression");
			if (activity.GetCustomProperty("SkipCompression") is bool skipCompressionValue)
			{
				base.OnStart(activity);
				return;
			}

			var bufferSize = _potentialCompressedSpans.Count;

			_logger.LogTrace("Current potential compressed span buffer size: {BufferSize}", bufferSize);

			// We also cap the buffer of in-flight spans
			if (bufferSize > MaxPotentialCompressedSpansBufferSize)
			{
				_logger.LogWarning("Potential compressed spans set is over the expected maximum limit of {SizeLimit}. Span compression is temporarily diabled.", MaxPotentialCompressedSpansBufferSize);
				base.OnStart(activity);
				return;
			}

			_logger.LogTrace("Adding {SpanId} as potentially compression eligable.", activity.SpanId);

			// As this current span to the in-flight potential compressed spans set.
			// NOTE: We use this hash set, rather than a property on each activity since that would require allocating an object and likely a dictionary per activity
			_potentialCompressedSpans.Add(activity.SpanId);
		}
		catch
		{
			// TODO
		}

		base.OnStart(activity);
	}

	/// <inheritdoc cref="OnStart"/>
	public override void OnEnd(Activity activity)
	{
		try
		{
			// We don't attempt to compress parent spans.
			// Note that replacement spans (created during flushing) will also have a null parent reference,
			// but they will have a parent ID.
			if (activity.Parent is null)
			{
				// Ensure we flush any buffered child activities before the parent ends
				FlushBuffer(activity);
				base.OnEnd(activity);
				return;
			}

			var isRecorded = activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded);

			// If this span can't be compressed, flush any buffered children it has and for its parent
			if (!isRecorded || !_potentialCompressedSpans.TryGetValue(activity.SpanId, out _))
			{
				// TODO - Logging
				FlushBuffersAndCallBase(activity);
				return;
			}

			// Ensure we don't keep this ID in memory any longer than needed once we've determined its eligablity
			_potentialCompressedSpans.Remove(activity.SpanId);

			if (activity.Links.Any())
			{
				// TODO - Logging
				FlushBuffersAndCallBase(activity);
				return;
			}

			if (activity.Baggage.Any())
			{
				// TODO - Logging
				FlushBuffersAndCallBase(activity);
				return;
			}

			// TODO - What about baggage, events and tags? When

			// We identify certain specific spans as those which we know may propogate tracecontext.
			// These are ineligable for span compression as compressing them may break distributed traces.
			// TODO: Add other ineligable spans here, such as messaging etc.
			if (activity.Kind == ActivityKind.Client && activity.TagObjects.Any(t => t.Key == "http.request.method"))
			{
				// TODO - Logging
				FlushBuffersAndCallBase(activity);
				return;
			}

			// If this span is not compression eligable, it can end normally and we flush any buffered spans.
			// Tracking for further potential compressed spans of the parent will continue for any subsequent spans.
			if (!IsCompressionEligible(activity))
			{
				// TODO - Logging
				FlushBuffersAndCallBase(activity);
				return;
			}

			if (_compressionBuffer.TryGetValue(activity.Parent, out var compressionBuffer))
			{
				// If there is already a buffered Activity for the parent, we can see if the
				// current Activity can be compressed with the previous sibling.
				if (!compressionBuffer.TryCompress(activity))
				{
					// TODO - Logging

					// If we were unable to compress, then we flush the buffer so that the
					// stored sibling is stopped and able to be recorded.
					FlushBuffer(activity.Parent);

					_compressionBuffer.Add(activity.Parent, activity);
					activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
				}
			}
			else
			{
				// TODO - Logging

				_compressionBuffer.Add(activity.Parent, activity);
				activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
			}
		}
		catch
		{
			// TODO
			// Should we also flush the buffer here?
		}

		base.OnEnd(activity);

		// Only successful (also unset as this implies success) spans can be compressed.
		// Additionaly, only those spans with durations under the configured limits may be compressed.
		static bool IsCompressionEligible(Activity activity) =>
			activity.Status is ActivityStatusCode.Ok or ActivityStatusCode.Unset
			&& activity.Duration < MaxSpanCompressionDuration;

		void FlushBuffersAndCallBase(Activity activity)
		{
			FlushBuffer(activity); // TODO: Do we need to do this as it should have no children to be eligable
			FlushBuffer(activity.Parent!); // FlushBuffers is only called on activities that have a parent
			base.OnEnd(activity);
		}
	}

	private void FlushBuffer(Activity activity)
	{
		if (!_compressionBuffer.TryGetValue(activity, out var compressionBuffer))
			return;

		_compressionBuffer.Remove(activity);

		var property = compressionBuffer.GetCustomProperty("Composite");
		var activityName = compressionBuffer.DisplayName;

		string? compressionStrategy = null;

		var compressionCount = 0;
		long compressionDuration = 0;

		if (property is Composite composite)
		{
			activityName = composite.ActivityName ?? $"[COMPOSITE] {compressionBuffer.DisplayName}";

			compressionStrategy = composite.CompressionStrategy;
			compressionCount = composite.Count;
			compressionDuration = composite.DurationSum;

			compressionBuffer.SetCustomProperty("Composite", null);
		}

		var newActivity = compressionBuffer.Source.StartActivity(activityName, compressionBuffer.Kind, compressionBuffer.Parent?.Context ?? default,
			compressionBuffer.TagObjects, compressionBuffer.Links, compressionBuffer.StartTimeUtc);

		if (newActivity is null)
		{
			// TODO - Log error
			return;
		}

		foreach (var @event in compressionBuffer.Events)
			newActivity.AddEvent(@event);

		foreach (var baggage in compressionBuffer.Baggage)
			newActivity.AddBaggage(baggage.Key, baggage.Value);

		foreach (var tag in compressionBuffer.TagObjects)
			newActivity.AddTag(tag.Key, tag.Value);

		newActivity.SetCustomProperty("SkipCompression", true);

		if (compressionStrategy is not null)
		{
			newActivity.SetTag("elastic.span_compression.strategy", compressionStrategy);
			newActivity.SetTag("elastic.span_compression.count", compressionCount);
			newActivity.SetTag("elastic.span_compression.duration", compressionDuration);
		}

		newActivity.ActivityTraceFlags = ActivityTraceFlags.Recorded;

		newActivity.SetEndTime(compressionBuffer.StartTimeUtc.Add(compressionBuffer.Duration));
		newActivity.Stop();
	}
}
