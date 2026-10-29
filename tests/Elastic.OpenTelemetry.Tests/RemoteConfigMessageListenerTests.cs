// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections;
using System.Reflection;
using Elastic.OpenTelemetry.OpAmp;
using Elastic.OpenTelemetry.OpAmp.Abstractions;
using Google.Protobuf;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry.OpAmp.Client.Messages;

namespace Elastic.OpenTelemetry.Tests;

/// <summary>
/// Tests for <see cref="RemoteConfigMessageListener.HandleMessage"/> verifying that subscribers are
/// notified exactly once for every remote config message, carrying a null log level when the
/// message does not contain a usable one. Without this, bootstrap would wait for the full
/// first-config timeout whenever the server responds without an applicable <c>log_level</c>.
/// </summary>
public class RemoteConfigMessageListenerTests
{
	[Fact]
	public void HandleMessage_NoElasticKey_NotifiesSubscriberWithNullLogLevel()
	{
		var (listener, subscriber) = CreateListenerWithSubscriber();

		listener.HandleMessage(CreateMessage());

		var config = Assert.Single(subscriber.Received);
		Assert.Null(config.LogLevel);
	}

	[Fact]
	public void HandleMessage_NonJsonContentType_NotifiesSubscriberWithNullLogLevel()
	{
		var (listener, subscriber) = CreateListenerWithSubscriber();

		listener.HandleMessage(CreateMessage(("elastic", """{"log_level":"debug"}""", "text/plain")));

		var config = Assert.Single(subscriber.Received);
		Assert.Null(config.LogLevel);
	}

	[Fact]
	public void HandleMessage_JsonWithoutLogLevel_NotifiesSubscriberWithNullLogLevel()
	{
		var (listener, subscriber) = CreateListenerWithSubscriber();

		listener.HandleMessage(CreateMessage(("elastic", """{"sampling_rate":0.5}""", "application/json")));

		var config = Assert.Single(subscriber.Received);
		Assert.Null(config.LogLevel);
	}

	[Fact]
	public void HandleMessage_ValidLogLevel_NotifiesSubscriberWithParsedLogLevel()
	{
		var (listener, subscriber) = CreateListenerWithSubscriber();

		listener.HandleMessage(CreateMessage(("elastic", """{"log_level":"debug"}""", "application/json")));

		var config = Assert.Single(subscriber.Received);
		Assert.Equal("debug", config.LogLevel);
	}

	[Fact]
	public void HandleMessage_UnknownLogLevelValue_NotifiesSubscriberWithRawValue()
	{
		// The listener only extracts the raw string; validation of the value happens later
		// in SetLogLevelFromCentralConfig, so an unknown value is still delivered as-is.
		var (listener, subscriber) = CreateListenerWithSubscriber();

		listener.HandleMessage(CreateMessage(("elastic", """{"log_level":"banana"}""", "application/json")));

		var config = Assert.Single(subscriber.Received);
		Assert.Equal("banana", config.LogLevel);
	}

	private static (RemoteConfigMessageListener Listener, RecordingSubscriber Subscriber) CreateListenerWithSubscriber()
	{
		var listener = new RemoteConfigMessageListener(NullLogger.Instance);
		var subscriber = new RecordingSubscriber();
		listener.Subscribe(subscriber);
		return (listener, subscriber);
	}

	// RemoteConfigMessage has an internal constructor that takes the generated OpAMP protobuf
	// AgentRemoteConfig type, which is also internal to the OpenTelemetry.OpAmp.Client package,
	// so the message has to be assembled via reflection.
	private static readonly Assembly OpAmpClientAssembly = typeof(RemoteConfigMessage).Assembly;
	private static readonly Type AgentRemoteConfigType = GetProtoType("AgentRemoteConfig");
	private static readonly Type AgentConfigMapType = GetProtoType("AgentConfigMap");
	private static readonly Type AgentConfigFileType = GetProtoType("AgentConfigFile");

	private static Type GetProtoType(string name) =>
		OpAmpClientAssembly.GetType($"OpAmp.Proto.V1.{name}", throwOnError: true)!;

	private static RemoteConfigMessage CreateMessage(params (string Key, string Body, string ContentType)[] entries)
	{
		var configMap = Activator.CreateInstance(AgentConfigMapType)!;
		var map = (IDictionary)AgentConfigMapType.GetProperty("ConfigMap")!.GetValue(configMap)!;

		foreach (var (key, body, contentType) in entries)
		{
			var file = Activator.CreateInstance(AgentConfigFileType)!;
			AgentConfigFileType.GetProperty("Body")!.SetValue(file, ByteString.CopyFromUtf8(body));
			AgentConfigFileType.GetProperty("ContentType")!.SetValue(file, contentType);
			map.Add(key, file);
		}

		var remoteConfig = Activator.CreateInstance(AgentRemoteConfigType)!;
		AgentRemoteConfigType.GetProperty("Config")!.SetValue(remoteConfig, configMap);

		return (RemoteConfigMessage)Activator.CreateInstance(
			typeof(RemoteConfigMessage),
			BindingFlags.Instance | BindingFlags.NonPublic,
			binder: null,
			args: [remoteConfig],
			culture: null)!;
	}

	private sealed class RecordingSubscriber : IOpAmpRemoteConfigMessageSubscriber
	{
		private readonly List<ElasticRemoteConfig> _received = [];

		public IReadOnlyList<ElasticRemoteConfig> Received => _received;

		public void HandleMessage(ElasticRemoteConfig elasticRemoteConfig) => _received.Add(elasticRemoteConfig);
	}
}
