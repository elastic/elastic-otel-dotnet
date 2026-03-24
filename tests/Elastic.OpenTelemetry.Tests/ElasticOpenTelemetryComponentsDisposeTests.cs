// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Core;
using Elastic.OpenTelemetry.Core.Configuration;
using Elastic.OpenTelemetry.Diagnostics;
using Elastic.OpenTelemetry.Tests.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Elastic.OpenTelemetry.Tests;

/// <summary>
/// Validates that <see cref="ElasticOpenTelemetryComponents.Dispose"/> and
/// <see cref="ElasticOpenTelemetryComponents.DisposeAsync"/> are thread-safe
/// and only dispose child components once.
/// </summary>
public class ElasticOpenTelemetryComponentsDisposeTests
{
	private static readonly ILogger Logger =
		NullLoggerFactory.Instance.CreateLogger<ElasticOpenTelemetryComponentsDisposeTests>();

	/// <summary>
	/// Creates components with a non-null <see cref="CentralConfiguration"/> backed by
	/// a <see cref="FaultingOpAmpClient"/> so callers can assert exactly-once dispose
	/// via the client's thread-safe counters.
	/// </summary>
	private static (ElasticOpenTelemetryComponents Components, FaultingOpAmpClient Client) CreateComponentsWithCentralConfig()
	{
		var options = new CompositeElasticOpenTelemetryOptions(new ElasticOpenTelemetryOptions());
		var logger = new CompositeLogger(options);
		var eventListener = new LoggingEventListener(logger, options);
		var client = new FaultingOpAmpClient();
		var centralConfig = new CentralConfiguration(client, Logger);
		var components = new ElasticOpenTelemetryComponents(logger, eventListener, options, centralConfig);
		return (components, client);
	}

	[Fact]
	public void Dispose_CalledTwice_DisposesChildrenOnce()
	{
		var (components, client) = CreateComponentsWithCentralConfig();

		components.Dispose();
		components.Dispose();

		Assert.Equal(1, client.StopCount);
		Assert.Equal(1, client.DisposeCount);
	}

	[Fact]
	public async Task DisposeAsync_CalledTwice_DisposesChildrenOnce()
	{
		var (components, client) = CreateComponentsWithCentralConfig();

		await components.DisposeAsync();
		await components.DisposeAsync();

		Assert.Equal(1, client.StopCount);
		Assert.Equal(1, client.DisposeCount);
	}

	[Fact]
	public async Task Dispose_ThenDisposeAsync_DisposesChildrenOnce()
	{
		var (components, client) = CreateComponentsWithCentralConfig();

		components.Dispose();
		await components.DisposeAsync();

		Assert.Equal(1, client.StopCount);
		Assert.Equal(1, client.DisposeCount);
	}

	[Fact]
	public async Task DisposeAsync_ThenDispose_DisposesChildrenOnce()
	{
		var (components, client) = CreateComponentsWithCentralConfig();

		await components.DisposeAsync();
		components.Dispose();

		Assert.Equal(1, client.StopCount);
		Assert.Equal(1, client.DisposeCount);
	}

	[Fact]
	public async Task Dispose_And_DisposeAsync_Concurrent_DisposesChildrenOnce()
	{
		var (components, client) = CreateComponentsWithCentralConfig();

		var barrier = new Barrier(2);
		var t1 = Task.Run(() => { barrier.SignalAndWait(); components.Dispose(); });
		var t2 = Task.Run(() => { barrier.SignalAndWait(); return components.DisposeAsync().AsTask(); });
		await Task.WhenAll(t1, t2);

		Assert.Equal(1, client.StopCount);
		Assert.Equal(1, client.DisposeCount);
	}

	[Fact]
	public async Task MultipleThreads_MixedDisposeAndDisposeAsync_DisposesChildrenOnce()
	{
		var (components, client) = CreateComponentsWithCentralConfig();

		var barrier = new Barrier(3);
		var t1 = Task.Run(() => { barrier.SignalAndWait(); components.Dispose(); });
		var t2 = Task.Run(() => { barrier.SignalAndWait(); components.Dispose(); });
		var t3 = Task.Run(() => { barrier.SignalAndWait(); return components.DisposeAsync().AsTask(); });
		await Task.WhenAll(t1, t2, t3);

		Assert.Equal(1, client.StopCount);
		Assert.Equal(1, client.DisposeCount);
	}
}
