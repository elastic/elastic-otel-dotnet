// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.OpAmp.Abstractions;

namespace Elastic.OpenTelemetry.Core.Configuration;

internal sealed class EmptyOpAmpClient : IOpAmpClient, IDisposable
{
	public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
	public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
	public void SubscribeToRemoteConfigMessages(IOpAmpRemoteConfigMessageSubscriber subscriber) { }

	public void Dispose() { }
}
