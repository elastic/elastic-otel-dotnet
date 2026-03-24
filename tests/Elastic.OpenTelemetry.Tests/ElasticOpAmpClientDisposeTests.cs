// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Core.Configuration;
using Elastic.OpenTelemetry.OpAmp;
using Microsoft.Extensions.Logging.Abstractions;

namespace Elastic.OpenTelemetry.Tests;

/// <summary>
/// Tests for the <see cref="ElasticOpAmpClient"/> HttpClient disposal workaround (HS-001).
/// </summary>
/// <remarks>
/// <b>Coverage notes — items verified by code review, not by test assertion:</b>
/// <list type="bullet">
/// <item><c>_httpClient.Dispose()</c> is called during <c>ElasticOpAmpClient.Dispose()</c> —
/// verified by inspection of the explicit <c>IDisposable.Dispose</c> implementation.
/// There is no spy or test-only constructor to assert this programmatically.</item>
/// <item>Constructor try/catch disposes <c>_httpClient</c> if <c>OpAmpClient</c> construction
/// or <c>Subscribe</c> throws — verified by inspection. Simulating a throw from the upstream
/// <c>OpAmpClient</c> constructor is not feasible without a test hook.</item>
/// </list>
/// </remarks>
public class ElasticOpAmpClientDisposeTests
{
	private static readonly ILogger Logger = NullLoggerFactory.Instance.CreateLogger<ElasticOpAmpClientDisposeTests>();

	[Fact]
	public void Dispose_CalledTwice_DoesNotThrow()
	{
		var client = new ElasticOpAmpClient(Logger, "http://localhost:1", "", "test-service", null, "test-ua");

		var exception = Record.Exception(() =>
		{
			((IDisposable)client).Dispose();
			((IDisposable)client).Dispose();
		});

		Assert.Null(exception);
	}

	/// <summary>
	/// Exercises the full timeout/fault → stop → dispose path using a real
	/// <see cref="ElasticOpAmpClient"/> and <see cref="HttpClient"/>. The
	/// <see cref="CentralConfiguration"/> constructor starts the client, which attempts to
	/// connect to a dead endpoint. This either faults (connection refused) or hits the 2 s
	/// timeout, triggering the cleanup path that disposes both the inner <c>OpAmpClient</c>
	/// and the <c>HttpClient</c>.
	/// </summary>
	/// <remarks>
	/// This test asserts that the cleanup path completes without throwing. It does <b>not</b>
	/// assert that <c>_httpClient</c> is disposed — that is covered by code review of
	/// <see cref="ElasticOpAmpClient"/>'s explicit <c>IDisposable.Dispose</c> implementation.
	/// <para/>
	/// <b>Fragile:</b> This test depends on the upstream <c>OpAmpClient</c>'s network behaviour
	/// against a dead endpoint. The dead endpoint may fail fast (connection refused) or slow
	/// (TCP timeout), depending on the OS and network stack. Either path exercises the cleanup
	/// code, but timing varies. If this test becomes flaky, it can be safely disabled.
	/// </remarks>
	[Fact]
	public void StartAsync_Faults_WithRealClient_CleansUpWithoutThrowing()
	{
		var client = new ElasticOpAmpClient(Logger, "http://localhost:1", "", "test-service", null, "test-ua");

		// CentralConfiguration.StartClient calls StartAsync, which connects to the dead
		// endpoint. This either faults or hits the 2 s timeout, triggering cleanup.
		var config = new CentralConfiguration(client, Logger);

		var exception = Record.Exception(() => config.Dispose());
		Assert.Null(exception);
	}
}
