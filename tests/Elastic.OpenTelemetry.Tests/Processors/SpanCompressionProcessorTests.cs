// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Extensions;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using Xunit.Abstractions;

namespace Elastic.OpenTelemetry.Tests.Processors;

public class SpanCompressionProcessorTests(ITestOutputHelper output)
{
	[Theory]
	[ClassData(typeof(SpanCompressionTestData))]
	public void SpanCompression_IsAppliedAsExpected(string scenarioName, Action<ActivitySource> act, Action<List<Activity>> assert)
	{
		output.WriteLine($"Starting {scenarioName}.");

		const string activitySourceName = nameof(SpanCompression_IsAppliedAsExpected);

		var logger = new TestLogger(output, LogLevel.Warning);
		var options = new ElasticOpenTelemetryBuilderOptions
		{
			Logger = logger,
			DistroOptions = new ElasticOpenTelemetryOptions()
			{
				SkipOtlpExporter = true,
				ElasticDefaults = ElasticDefaults.None,
				LogTargets = LogTargets.None
			}
		};

		var activitySource = new ActivitySource(activitySourceName, "1.0.0");
		var exportedItems = new List<Activity>();

		using var session = new ElasticOpenTelemetryBuilder(options)
			.WithTracing(tracing =>
			{
				tracing
					.ConfigureResource(rb => rb.AddService("Test", "1.0.0"))
					.AddSource(activitySourceName)
					.AddProcessor(new SpanCompressionProcessor(logger))
					.AddInMemoryExporter(exportedItems);
			})
			.Build();

		act.Invoke(activitySource);
		assert.Invoke(exportedItems);

		logger.Messages.Should().BeEmpty();
	}

	public class SpanCompressionTestData : TheoryData<string, Action<ActivitySource>, Action<List<Activity>>>
	{
		public SpanCompressionTestData()
		{
			Add(
			"Scenario 1 - Single child span which is compression eligable",
			source =>
			{
				// A parent span with a single child that is eligable for compression.
				// We expect the child to have been buffered and flushed when the parent ends.

				var activity1 = source.StartActivity(name: "Parent Span", kind: ActivityKind.Server, startTime: new DateTime(2024, 12, 17, 10, 00, 00, 000, DateTimeKind.Utc));
				var activity2 = source.StartActivity(name: "Child Span", kind: ActivityKind.Client, startTime: new DateTime(2024, 12, 17, 10, 00, 00, 005, DateTimeKind.Utc));

				activity2?.AddEvent(new("ACustomEvent", new DateTime(2024, 12, 17, 10, 00, 00, 010, DateTimeKind.Utc), new ActivityTagsCollection([new("tag1", 100)])));

				activity2?.AddTag("ActivityTag1", "ActivityTagValue1");
				activity2?.AddTag("ActivityTag2", 1000);

				activity2?.SetEndTime(new DateTime(2024, 12, 17, 10, 00, 00, 025, DateTimeKind.Utc));
				activity2?.Stop();

				activity1?.SetEndTime(new DateTime(2024, 12, 17, 10, 00, 00, 200, DateTimeKind.Utc));
				activity1?.Stop();
			},
			exported =>
			{
				// The child should be received first as it is flushed when the parent ends.
				// The replacement child should contain all links, events, baggage and tags from the original Activity.
				// The replacement child should be skipped for further compression via the expected property.

				exported.Count.Should().Be(2);

				var childActivity = exported[0];
				var parentActivity = exported[1];

				childActivity.DisplayName.Should().Be("Child Span");
				parentActivity.DisplayName.Should().Be("Parent Span");

				childActivity.Duration.Should().Be(TimeSpan.FromMilliseconds(020));
				parentActivity.Duration.Should().Be(TimeSpan.FromMilliseconds(200));

				var eventOne = childActivity.Events.Should().ContainSingle().Subject;
				eventOne.Name.Should().Be("ACustomEvent");
				eventOne.Timestamp.Should().Be(new DateTime(2024, 12, 17, 10, 00, 00, 010, DateTimeKind.Utc));
				var eventOneTag = eventOne.Tags.Should().ContainSingle().Subject;
				eventOneTag.Key.Should().Be("tag1");
				eventOneTag.Value.Should().Be(100);

				var tagOne = childActivity.GetTagItem("ActivityTag1");
				tagOne.Should().NotBeNull();
				tagOne.Should().BeOfType<string>().Subject.Should().Be("ActivityTagValue1");

				var tagTwo = childActivity.GetTagItem("ActivityTag2");
				tagTwo.Should().NotBeNull();
				tagTwo.Should().BeOfType<int>().Subject.Should().Be(1000);

				AssertSkipCompressionProperty(childActivity);
			});

			Add(
			"Scenario 2 - Child span using RecordException",
			source =>
			{
				// A parent span with a single child that has a recorded exception (via the OTel API).
				// We expect the child to have been buffered and flushed when the parent ends.

				var activity1 = source.StartActivity(name: "Parent Span", kind: ActivityKind.Server, startTime: new DateTime(2024, 12, 17, 10, 00, 00, 000, DateTimeKind.Utc));
				var activity2 = source.StartActivity(name: "Child Span", kind: ActivityKind.Client, startTime: new DateTime(2024, 12, 17, 10, 00, 00, 005, DateTimeKind.Utc));

				activity2?.RecordException(new Exception("This is a test exception"), [new("tag1", "value1")]);

				activity2?.SetEndTime(new DateTime(2024, 12, 17, 10, 00, 00, 025, DateTimeKind.Utc));
				activity2?.Stop();

				activity1?.SetEndTime(new DateTime(2024, 12, 17, 10, 00, 00, 200, DateTimeKind.Utc));
				activity1?.Stop();
			},
			exported =>
			{
				// The child should be received first as it is flushed when the parent ends.
				// The replacement child should contain an exception event.
				// The replacement child should be skipped for further compression via the expected property.

				exported.Count.Should().Be(2);

				var childActivity = exported[0];
				var parentActivity = exported[1];

				childActivity.DisplayName.Should().Be("Child Span");
				parentActivity.DisplayName.Should().Be("Parent Span");

				childActivity.Duration.Should().Be(TimeSpan.FromMilliseconds(020));
				parentActivity.Duration.Should().Be(TimeSpan.FromMilliseconds(200));

				childActivity.TagObjects.Should().BeEmpty();
				childActivity.Links.Should().BeEmpty();
				childActivity.Baggage.Should().BeEmpty();

				var exceptionEvent = childActivity.Events.Should().ContainSingle().Subject;

				exceptionEvent.Name.Should().Be("exception");
				exceptionEvent.Timestamp.Should().BeBefore(DateTime.UtcNow); // We can't be more specific on this, but it is sufficient.
				exceptionEvent.Tags.Should().HaveCount(4); // Three from the OTel library + one from our custom tag.

				var tag = exceptionEvent.Tags.SingleOrDefault(t => t.Key.Equals("exception.type", StringComparison.OrdinalIgnoreCase));
				tag.Value.Should().Be("System.Exception");

				tag = exceptionEvent.Tags.SingleOrDefault(t => t.Key.Equals("exception.stacktrace", StringComparison.OrdinalIgnoreCase));
				tag.Value.Should().Be("System.Exception: This is a test exception");

				tag = exceptionEvent.Tags.SingleOrDefault(t => t.Key.Equals("exception.message", StringComparison.OrdinalIgnoreCase));
				tag.Value.Should().Be("This is a test exception");

				tag = exceptionEvent.Tags.SingleOrDefault(t => t.Key.Equals("tag1", StringComparison.OrdinalIgnoreCase));
				tag.Value.Should().Be("value1");

				AssertSkipCompressionProperty(childActivity);
			});

			Add(
			"Scenario 3 - Child span using AddException",
			source =>
			{
				// A parent span with a single child that has an via the (9.0.0+) AddException method.
				// We expect the child to have been buffered and flushed when the parent ends.

				var activity1 = source.StartActivity(name: "Parent Span", kind: ActivityKind.Server, startTime: new DateTime(2024, 12, 17, 10, 00, 00, 000, DateTimeKind.Utc));
				var activity2 = source.StartActivity(name: "Child Span", kind: ActivityKind.Client, startTime: new DateTime(2024, 12, 17, 10, 00, 00, 005, DateTimeKind.Utc));

				activity2?.AddException(new Exception("This is a test exception"), [new("tag1", "value1")], new DateTime(2024, 12, 17, 10, 00, 00, 015, DateTimeKind.Utc));

				activity2?.SetEndTime(new DateTime(2024, 12, 17, 10, 00, 00, 025, DateTimeKind.Utc));
				activity2?.Stop();

				activity1?.SetEndTime(new DateTime(2024, 12, 17, 10, 00, 00, 200, DateTimeKind.Utc));
				activity1?.Stop();
			},
			exported =>
			{
				// The child should be received first as it is flushed when the parent ends.
				// The replacement child should contain an exception event.
				// The replacement child should be skipped for further compression via the expected property.

				exported.Count.Should().Be(2);

				var childActivity = exported[0];
				var parentActivity = exported[1];

				childActivity.DisplayName.Should().Be("Child Span");
				parentActivity.DisplayName.Should().Be("Parent Span");

				childActivity.Duration.Should().Be(TimeSpan.FromMilliseconds(020));
				parentActivity.Duration.Should().Be(TimeSpan.FromMilliseconds(200));

				childActivity.TagObjects.Should().BeEmpty();
				childActivity.Links.Should().BeEmpty();
				childActivity.Baggage.Should().BeEmpty();

				var exceptionEvent = childActivity.Events.Should().ContainSingle().Subject;

				exceptionEvent.Name.Should().Be("exception");
				exceptionEvent.Timestamp.Should().Be(new DateTime(2024, 12, 17, 10, 00, 00, 015, DateTimeKind.Utc));
				exceptionEvent.Tags.Should().HaveCount(4); // Three from System.Diagnostics.DiagnosticSOurce + one from our custom tag.

				var tag = exceptionEvent.Tags.SingleOrDefault(t => t.Key.Equals("exception.type", StringComparison.OrdinalIgnoreCase));
				tag.Value.Should().Be("System.Exception");

				tag = exceptionEvent.Tags.SingleOrDefault(t => t.Key.Equals("exception.stacktrace", StringComparison.OrdinalIgnoreCase));
				tag.Value.Should().Be("System.Exception: This is a test exception");

				tag = exceptionEvent.Tags.SingleOrDefault(t => t.Key.Equals("exception.message", StringComparison.OrdinalIgnoreCase));
				tag.Value.Should().Be("This is a test exception");

				tag = exceptionEvent.Tags.SingleOrDefault(t => t.Key.Equals("tag1", StringComparison.OrdinalIgnoreCase));
				tag.Value.Should().Be("value1");

				AssertSkipCompressionProperty(childActivity);
			});

			Add(
			"Scenario 4 - Three child spans meeting exact match criteria.",
			source =>
			{
				// A parent span with a three child spans, all compression eligable and using the same name (exact match).
				// We expect a single composite span to have been buffered and flushed when the parent ends.

				var activity1 = source.StartActivity(name: "Parent Span", kind: ActivityKind.Server, startTime: new DateTime(2024, 12, 17, 10, 00, 00, 000, DateTimeKind.Utc));

				source.StartActivity(name: "Child Span", kind: ActivityKind.Client, startTime: new DateTime(2024, 12, 17, 10, 00, 00, 005, DateTimeKind.Utc))
					?.SetEndTime(new DateTime(2024, 12, 17, 10, 00, 00, 025, DateTimeKind.Utc))
					?.Stop();

				source.StartActivity(name: "Child Span", kind: ActivityKind.Client, startTime: new DateTime(2024, 12, 17, 10, 00, 00, 050, DateTimeKind.Utc))
					?.SetEndTime(new DateTime(2024, 12, 17, 10, 00, 00, 060, DateTimeKind.Utc))
					?.Stop();

				source.StartActivity(name: "Child Span", kind: ActivityKind.Client, startTime: new DateTime(2024, 12, 17, 10, 00, 00, 080, DateTimeKind.Utc))
					?.SetEndTime(new DateTime(2024, 12, 17, 10, 00, 00, 110, DateTimeKind.Utc))
					?.Stop();

				activity1?.SetEndTime(new DateTime(2024, 12, 17, 10, 00, 00, 200, DateTimeKind.Utc));
				activity1?.Stop();
			},
			exported =>
			{
				// The composite span should be received first as it is flushed when the parent ends.

				exported.Count.Should().Be(2);

				var childActivity = exported[0];
				var parentActivity = exported[1];

				childActivity.DisplayName.Should().Be("[COMPOSITE] Child Span");
				parentActivity.DisplayName.Should().Be("Parent Span");

				childActivity.Duration.Should().Be(TimeSpan.FromMilliseconds(105)); // Should be the duration from the start of the first span, to the end of the final span
				parentActivity.Duration.Should().Be(TimeSpan.FromMilliseconds(200));

				childActivity.Links.Should().BeEmpty();
				childActivity.Baggage.Should().BeEmpty();

				childActivity.TagObjects.Should().HaveCount(3);

				var compressionStrategy = childActivity.TagObjects.Single(t => t.Key.Equals("elastic.span_compression.strategy", StringComparison.Ordinal));
				compressionStrategy.Value.Should().Be("exact_match");

				var compressionCount = childActivity.TagObjects.Single(t => t.Key.Equals("elastic.span_compression.count", StringComparison.Ordinal));
				compressionCount.Value.Should().Be(3);

				var compressionDuration = childActivity.TagObjects.Single(t => t.Key.Equals("elastic.span_compression.duration", StringComparison.Ordinal));
				compressionDuration.Value.Should().Be(60);

				AssertSkipCompressionProperty(childActivity);
			});

			Add(
			"Scenario 5 - Child span with a span link",
			source =>
			{
				// A parent span with a single child that is ineligable for compression because it contains a span link.
				// We expect the child not to be buffered and exported first.

				var activity1 = source.StartActivity(name: "Parent Span", kind: ActivityKind.Server, startTime: new DateTime(2024, 12, 17, 10, 00, 00, 000, DateTimeKind.Utc));
				var activity2 = source.StartActivity(name: "Child Span", kind: ActivityKind.Client, startTime: new DateTime(2024, 12, 17, 10, 00, 00, 005, DateTimeKind.Utc));

				activity2?.AddLink(new ActivityLink(new ActivityContext(
					ActivityTraceId.CreateFromString("4bf92f3577b34da6a3ce929d0e0e4736"),
					ActivitySpanId.CreateFromString("00f067aa0ba902b7"),
					ActivityTraceFlags.Recorded, "es=1", true)));

				activity2?.SetEndTime(new DateTime(2024, 12, 17, 10, 00, 00, 025, DateTimeKind.Utc));
				activity2?.Stop();

				activity1?.SetEndTime(new DateTime(2024, 12, 17, 10, 00, 00, 200, DateTimeKind.Utc));
				activity1?.Stop();
			},
			exported =>
			{
				// The child should be received first as it is not buffered.
				// As the child was not buffered and is therefore the original, it should not contain the skip compression property.

				exported.Count.Should().Be(2);

				var childActivity = exported[0];
				var parentActivity = exported[1];

				childActivity.DisplayName.Should().Be("Child Span");
				parentActivity.DisplayName.Should().Be("Parent Span");

				childActivity.Duration.Should().Be(TimeSpan.FromMilliseconds(020));
				parentActivity.Duration.Should().Be(TimeSpan.FromMilliseconds(200));

				var link = childActivity.Links.Should().ContainSingle().Subject;
				link.Context.TraceId.Should().Be(ActivityTraceId.CreateFromString("4bf92f3577b34da6a3ce929d0e0e4736"));
				link.Context.SpanId.Should().Be(ActivitySpanId.CreateFromString("00f067aa0ba902b7"));
				link.Context.TraceState.Should().Be("es=1");
				link.Context.IsRemote.Should().BeTrue();

				AssertNoSkipCompressionProperty(childActivity);
			});

			Add(
			"Scenario 6 - Child span with baggage",
			source =>
			{
				// A parent span with a single child that is ineligable for compression because it contains baggage.
				// We expect the child not to be buffered and exported first.

				var activity1 = source.StartActivity(name: "Parent Span", kind: ActivityKind.Server, startTime: new DateTime(2024, 12, 17, 10, 00, 00, 000, DateTimeKind.Utc));
				var activity2 = source.StartActivity(name: "Child Span", kind: ActivityKind.Client, startTime: new DateTime(2024, 12, 17, 10, 00, 00, 005, DateTimeKind.Utc));

				activity2?.AddBaggage("BaggageItem1", "BaggageValue1");

				activity2?.SetEndTime(new DateTime(2024, 12, 17, 10, 00, 00, 025, DateTimeKind.Utc));
				activity2?.Stop();

				activity1?.SetEndTime(new DateTime(2024, 12, 17, 10, 00, 00, 200, DateTimeKind.Utc));
				activity1?.Stop();
			},
			exported =>
			{
				// The child should be received first as it is flushed when the parent ends.
				// The replacement child should contain all links, events, baggage and tags from the original Activity.
				// The replacement child should be skipped for further compression via the expected property.

				exported.Count.Should().Be(2);

				var childActivity = exported[0];
				var parentActivity = exported[1];

				childActivity.DisplayName.Should().Be("Child Span");
				parentActivity.DisplayName.Should().Be("Parent Span");

				childActivity.Duration.Should().Be(TimeSpan.FromMilliseconds(020));
				parentActivity.Duration.Should().Be(TimeSpan.FromMilliseconds(200));

				var baggage = childActivity.Baggage.Should().ContainSingle().Subject;
				baggage.Key.Should().Be("BaggageItem1");
				baggage.Value.Should().Be("BaggageValue1");

				AssertNoSkipCompressionProperty(childActivity);
			});

			_ = 1;

			Add(
			"Scenario 7 - Three child spans with the second being ineligable for compression due to error status",
			source =>
			{
				// A parent span with a single child that is eligable for compression.
				// We expect the child to have been buffered and flushed when the parent ends.

				var activity1 = source.StartActivity(name: "Parent Span", kind: ActivityKind.Server, startTime: new DateTime(2024, 12, 17, 10, 00, 00, 000, DateTimeKind.Utc));

				source.StartActivity(name: "Child Span", kind: ActivityKind.Client, startTime: new DateTime(2024, 12, 17, 10, 00, 00, 005, DateTimeKind.Utc))
					?.SetEndTime(new DateTime(2024, 12, 17, 10, 00, 00, 025, DateTimeKind.Utc))
					?.Stop();

				var childWithException = source.StartActivity(name: "Child Span", kind: ActivityKind.Client, startTime: new DateTime(2024, 12, 17, 10, 00, 00, 050, DateTimeKind.Utc));
				childWithException?.SetStatus(ActivityStatusCode.Error, "An error occurred!");
				childWithException?.SetEndTime(new DateTime(2024, 12, 17, 10, 00, 00, 060, DateTimeKind.Utc));
				childWithException?.Stop();

				source.StartActivity(name: "Child Span", kind: ActivityKind.Client, startTime: new DateTime(2024, 12, 17, 10, 00, 00, 080, DateTimeKind.Utc))
					?.SetEndTime(new DateTime(2024, 12, 17, 10, 00, 00, 110, DateTimeKind.Utc))
					?.Stop();

				activity1?.SetEndTime(new DateTime(2024, 12, 17, 10, 00, 00, 200, DateTimeKind.Utc));
				activity1?.Stop();
			},
			exported =>
			{
				exported.Count.Should().Be(4);

				var childActivity1 = exported[0];
				var childActivity2 = exported[1];
				var childActivity3 = exported[2];
				var parentActivity = exported[3];

				childActivity1.DisplayName.Should().Be("Child Span");
				childActivity2.DisplayName.Should().Be("Child Span");
				childActivity3.DisplayName.Should().Be("Child Span");
				parentActivity.DisplayName.Should().Be("Parent Span");

				childActivity1.Duration.Should().Be(TimeSpan.FromMilliseconds(020));
				childActivity2.Duration.Should().Be(TimeSpan.FromMilliseconds(010));
				childActivity3.Duration.Should().Be(TimeSpan.FromMilliseconds(030));
				parentActivity.Duration.Should().Be(TimeSpan.FromMilliseconds(200));

				childActivity1.Status.Should().Be(ActivityStatusCode.Unset);
				childActivity2.Status.Should().Be(ActivityStatusCode.Error);
				childActivity3.Status.Should().Be(ActivityStatusCode.Unset);

				childActivity1.ParentId.Should().Be(parentActivity.Id);
				childActivity2.Parent.Should().NotBeNull();
				childActivity2.Parent!.Id.Should().Be(parentActivity.Id);
				//childActivity2.ParentId.Should().Be(parentActivity.Id); Is this a bug in Activity?
				childActivity3.ParentId.Should().Be(parentActivity.Id);

				childActivity1.ActivityTraceFlags.Should().HaveFlag(ActivityTraceFlags.Recorded);
				childActivity2.ActivityTraceFlags.Should().HaveFlag(ActivityTraceFlags.Recorded);
				childActivity3.ActivityTraceFlags.Should().HaveFlag(ActivityTraceFlags.Recorded);

				AssertSkipCompressionProperty(childActivity1);
				AssertNoSkipCompressionProperty(childActivity2);
				AssertSkipCompressionProperty(childActivity3);
			});

			// TODO - 1st eligable, 2nd with child, then three eligable
			// TODO - Same kind tests
			// TODO - SetStatus using the OTel extension method - We should check their custom attributes and exclude failed activities
		}

		private static void AssertSkipCompressionProperty(Activity activity)
		{
			var skipCompressionProperty = activity.GetCustomProperty("SkipCompression");
			skipCompressionProperty.Should().NotBeNull();
			skipCompressionProperty.Should().BeOfType<bool>().Subject.Should().BeTrue();
		}

		private static void AssertNoSkipCompressionProperty(Activity activity)
		{
			var skipCompressionProperty = activity.GetCustomProperty("SkipCompression");
			skipCompressionProperty.Should().BeNull();
		}
	}
}
