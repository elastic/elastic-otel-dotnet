// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.OpenTelemetry.OpAmp.Abstractions;

/// <summary>
/// Abstraction for subscribing to OpAmp messages using only primitive types.
/// This interface crosses AssemblyLoadContext boundaries safely because it uses only primitives.
/// </summary>
public interface IOpAmpMessageSubscriber
{
	/// <summary>
	/// Event raised when a message is received from the OpAmp server.
	/// </summary>
	/// <param name="messageType">The fully-qualified name of the message type (e.g., "RemoteConfigMessage")</param>
	/// <param name="jsonPayload">The message payload serialized as JSON bytes using System.Text.Json</param>
	event Action<string, byte[]>? MessageReceived;

	/// <summary>
	/// Event raised when the connection state changes.
	/// </summary>
	event Action<bool>? ConnectionChanged;

	/// <summary>
	/// Starts the OpAmp client and connects to the server.
	/// </summary>
	Task StartAsync(string endpoint, CancellationToken cancellationToken = default);

	/// <summary>
	/// Stops the OpAmp client and closes the connection.
	/// </summary>
	Task StopAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets whether the client is currently connected to the OpAmp server.
	/// </summary>
	bool IsConnected { get; }
}
