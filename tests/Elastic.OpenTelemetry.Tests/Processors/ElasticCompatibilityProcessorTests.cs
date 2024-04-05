// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.SemanticConventions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Elastic.OpenTelemetry.Tests.Processors;

public sealed class ElasticCompatibilityProcessorTests : IDisposable
{
	private readonly ActivitySource _activitySource;
	private readonly ActivityListener _listener;

	public ElasticCompatibilityProcessorTests()
	{
		// It's a bit annoying as we have to create an ActivitySource and ActivityListener
		// just so we can create an Activity with the ActivityKind set. The Activity ctor
		// doesn't allow us to set the ActivityKind directly.
		_activitySource = new ActivitySource("test");

		// We need a listener so that CreateActivity returns a non-null Activity
		_listener = new ActivityListener()
		{
			ShouldListenTo = _ => true,
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
		};

		ActivitySource.AddActivityListener(_listener);
	}

	[Fact]
	public void AddsExpectedNetHostAttributes()
	{
		const string hostName = "/";
		const int port = 80;

		var activity = _activitySource.CreateActivity("test", ActivityKind.Server)!;

		activity.SetTag(TraceSemanticConventions.ServerAddress, hostName);
		activity.SetTag(TraceSemanticConventions.ServerPort, port);

		var sut = new ElasticCompatibilityProcessor(NullLogger.Instance);
		sut.OnEnd(activity);

		// We can test with Tags (rather than TagObjects) here as we know these are string values
		activity.Tags.Single(t => t.Key == TraceSemanticConventions.NetHostName).Value.Should().Be(hostName);

		activity.TagObjects.Single(t => t.Key == TraceSemanticConventions.NetHostPort).Value
			.Should().BeOfType<int>().Subject.Should().Be(port);
	}

	[Fact]
	public void AddsExpectedHttpAttributes_WhenUrlQuery_IsNotPresent()
	{
		const string scheme = "https";
		const string path = "/my/path";

		var activity = _activitySource.CreateActivity("test", ActivityKind.Server)!;

		activity.SetTag(TraceSemanticConventions.UrlScheme, scheme);
		activity.SetTag(TraceSemanticConventions.UrlPath, path);

		var sut = new ElasticCompatibilityProcessor(NullLogger.Instance);
		sut.OnEnd(activity);

		// We can test with Tags (rather than TagObjects) here as we know these are string values
		activity.Tags.Single(t => t.Key == TraceSemanticConventions.HttpScheme).Value.Should().Be(scheme);
		activity.Tags.Single(t => t.Key == TraceSemanticConventions.HttpTarget).Value.Should().Be(path);
	}

	[Fact]
	public void AddsExpectedHttpAttributes_WhenUrlQuery_IsPresent()
	{
		const string scheme = "https";
		const string path = "/my/path";
		const string query = "q=OpenTelemetry";

		var activity = _activitySource.CreateActivity("test", ActivityKind.Server)!;

		activity.SetTag(TraceSemanticConventions.UrlScheme, scheme);
		activity.SetTag(TraceSemanticConventions.UrlPath, path);
		activity.SetTag(TraceSemanticConventions.UrlQuery, query);

		var sut = new ElasticCompatibilityProcessor(NullLogger.Instance);
		sut.OnEnd(activity);

		// We can test with Tags (rather than TagObjects) here as we know these are string values
		activity.Tags.Single(t => t.Key == TraceSemanticConventions.HttpScheme).Value.Should().Be(scheme);
		activity.Tags.Single(t => t.Key == TraceSemanticConventions.HttpTarget).Value.Should().Be($"{path}?{query}");
	}

	public void Dispose()
	{
		_activitySource.Dispose();
		_listener.Dispose();
	}
}
