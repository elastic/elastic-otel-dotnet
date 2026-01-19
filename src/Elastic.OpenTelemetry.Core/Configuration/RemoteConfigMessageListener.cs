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

	/// <summary>
	/// Handles messages received from the isolated OpAmp abstractions layer.
	/// These are JSON-serialized payloads that cross the ALC boundary safely.
	/// </summary>
	internal void HandleMessage(string messageType, byte[] jsonPayload)
	{
		try
		{
			if (messageType == "RemoteConfigMessage")
			{
				// The payload is JSON-serialized from the isolated ALC
				// For now, log that we received it
				// In a full implementation, you would deserialize and create the RemoteConfigMessage
				var json = System.Text.Encoding.UTF8.GetString(jsonPayload);
				System.Diagnostics.Debug.WriteLine($"Received RemoteConfigMessage payload: {json}");
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error handling message: {ex.Message}");
		}
	}

	internal Task<RemoteConfigMessage> FirstMessageReceivedTask => _firstMessageReceived.Task;
}

#if NET8_0_OR_GREATER
/// <summary>
/// Bridge adapter for RemoteConfigMessageListener to work with isolated ALC OpAmpClient.
/// 
/// This is needed because RemoteConfigMessageListener implements IOpAmpListener from the default ALC,
/// but the isolated ALCs OpAmpClient expects listeners from the isolated ALC. These are different types across ALCs.
/// 
/// This adapter uses dynamic invocation to forward HandleMessage calls across the ALC boundary.
/// </summary>
internal class IsolatedALCRemoteConfigMessageListenerBridge
{
	private readonly RemoteConfigMessageListener _listener;

	internal IsolatedALCRemoteConfigMessageListenerBridge(RemoteConfigMessageListener listener)
		=> _listener = listener;

	/// <summary>
	/// This method will be called by the isolated ALCs OpAmpClient.
	/// The message parameter will be of type RemoteConfigMessage from the isolated ALC,
	/// but we forward it to the listener which expects the default ALCs RemoteConfigMessage.
	/// 
	/// This works because the underlying RemoteConfigMessage objects are structurally identical.
	/// </summary>
	public void HandleMessage(dynamic message)
	{
		try
		{
			// Forward to listener - works because messages are structurally identical across ALCs
			_listener.HandleMessage((RemoteConfigMessage)message);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"IsolatedALCRemoteConfigMessageListenerBridge.HandleMessage failed: {ex.Message}");
		}
	}
}
#endif
