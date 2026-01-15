// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using OpenTelemetry.OpAmp.Client.Listeners;
using OpenTelemetry.OpAmp.Client.Messages;

#if NETFRAMEWORK
using System.Net.Http;
#endif

namespace Elastic.OpenTelemetry.Core.Configuration;

internal class RemoteConfigMessageListener : IOpAmpListener<RemoteConfigMessage>
{
	private readonly TaskCompletionSource<RemoteConfigMessage> _firstMessageReceived = new();

	public void HandleMessage(RemoteConfigMessage message) => _firstMessageReceived.TrySetResult(message);

	internal Task<RemoteConfigMessage> FirstMessageReceivedTask => _firstMessageReceived.Task;
}
