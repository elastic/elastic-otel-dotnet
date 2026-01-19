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

#if NET8_0_OR_GREATER
/// <summary>
/// Bridge adapter for RemoteConfigMessageListener to work with isolated ALC OpAmpClient.
/// This is needed because RemoteConfigMessageListener implements IOpAmpListener from the default ALC,
/// but the isolated ALCs OpAmpClient expects listeners from the isolated ALC.
/// These are different types across ALCs, so this adapter uses dynamic invocation to forward calls.
/// </summary>
[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Dynamic invocation is only used when IsDynamicCodeSupported is true")]
internal class IsolatedALCRemoteConfigMessageListenerBridge
{
	private readonly RemoteConfigMessageListener _listener;

	internal IsolatedALCRemoteConfigMessageListenerBridge(RemoteConfigMessageListener listener) => _listener = listener;

	/// <summary>
	/// Forwards HandleMessage calls from the isolated ALC to the default ALC listener.
	/// The message parameter is from the isolated ALC but is structurally identical.
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
