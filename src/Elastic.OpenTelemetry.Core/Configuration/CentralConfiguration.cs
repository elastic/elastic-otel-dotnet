// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Elastic.OpenTelemetry.Configuration;
using Microsoft.Extensions.Logging;
using OpenTelemetry.OpAmp.Client;
using OpenTelemetry.OpAmp.Client.Messages;
using System.Reflection;


#if NETFRAMEWORK
using System.Net.Http;
#endif

#if NET8_0_OR_GREATER //&& USE_ISOLATED_OPAMP_CLIENT
using System.Runtime.CompilerServices;
#else
using OpenTelemetry.OpAmp.Client.Settings;
#endif

namespace Elastic.OpenTelemetry.Core.Configuration;

internal interface IOpAmpClient : IDisposable
{
	Task StartAsync(CancellationToken cancellationToken = default);
}

internal sealed class WrappedOpAmpClient : IOpAmpClient
{
	private readonly OpAmpClient _client;

	internal WrappedOpAmpClient(OpAmpClient client) => _client = client;

	public Task StartAsync(CancellationToken cancellationToken = default) => _client.StartAsync(cancellationToken);

	public void Dispose() => _client.Dispose();
}

#if NET8_0_OR_GREATER
[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Dynamic invocations are only used when IsDynamicCodeSupported is true")]
internal sealed class IsolatedOpAmpClientWrapper : IOpAmpClient
{
	private readonly dynamic _client;

	internal IsolatedOpAmpClientWrapper(dynamic client) => _client = client;

	public Task StartAsync(CancellationToken cancellationToken = default) => _client.StartAsync(cancellationToken);

	public void Dispose() => _client.Dispose();
}
#endif

internal sealed class EmptyOpAmpClient : IOpAmpClient
{
	public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

	public void Dispose()
	{
		// No-op
	}
}

internal sealed class CentralConfiguration : IDisposable, IAsyncDisposable
{
	private const int OpAmpBlockingStartTimeoutMilliseconds = 500;

	private static string UserAgent => $"elastic-opamp-dotnet/{VersionHelper.InformationalVersion}";

	private readonly ILogger _logger;
	private readonly IOpAmpClient _client;
	private readonly RemoteConfigMessageListener _remoteConfigListener;
	private readonly ConcurrentBag<ICentralConfigurationSubscriber> _subscribers = [];
	private readonly CancellationTokenSource _startupCancellationTokenSource;
	private readonly Task _startupTask;

	private volatile bool _isStarted;
	private volatile bool _startupFailed;
	private bool _disposed;

	[UnconditionalSuppressMessage("AssemblyLoadTrimming", "IL2026: RequiresUnreferencedCode", Justification = "Reflection calls" +
	 " are guarded by a RuntimeFeature.IsDynamicCodeSupported` check and therefore this method is safe to call in AoT scenarios")]
	internal CentralConfiguration(CompositeElasticOpenTelemetryOptions options, ILogger logger)
	{
		_logger = logger;
		_startupCancellationTokenSource = new CancellationTokenSource();
		_remoteConfigListener = new RemoteConfigMessageListener();

		_logger.LogDebug("CentralConfiguration constructor starting OpAmp enabled check");

		if (options.IsOpAmpEnabled() is false)
		{
			_client = new EmptyOpAmpClient();
			_startupTask = Task.CompletedTask;
			_isStarted = false;

			_logger.LogDebug("OpAmp is not enabled in the provided options. Central Configuration will not be initialized");

			return;
		}

		_logger.LogInformation("Initializing Central Configuration with OpAmp endpoint: {OpAmpEndpoint}", options.OpAmpEndpoint);

#if NET8_0_OR_GREATER //&& USE_ISOLATED_OPAMP_CLIENT
		if (!RuntimeFeature.IsDynamicCodeSupported)
		{
			_client = new EmptyOpAmpClient();
			_startupTask = Task.CompletedTask;
			_isStarted = false;
			_startupFailed = true;

			_logger.LogError("Dynamic code is not supported on the current runtime. OpAmp client will not be initialized.");

			return;
		}

		try
		{
			// Initialize isolated loading of OpAmp dependencies to prevent version conflicts
			_logger.LogDebug("Initializing OpAmp client in isolated load context to prevent dependency version conflicts.");

			var loadContext = new OpAmpLoadContext(logger);
			_logger.LogDebug("OpAmpLoadContext created successfully");

			string[] assembliesToLoad = [
				"Google.Protobuf.dll",
				"OpenTelemetry.OpAmp.Client.dll",
				"Elastic.OpenTelemetry.OpAmp.Abstractions.dll",
			];

			Assembly? abstractionsAssembly = null;

			foreach (var assembly in assembliesToLoad)
			{
				// Load the abstractions assembly from the isolated ALC
				// This contains the OpAmpMessageSubscriber implementation that handles OpAmp types
				var abstractionsAssemblyPath = Path.Combine(
					loadContext.OtelInstallationPath ?? "",
					"Elastic.OpenTelemetry.OpAmp.Abstractions.dll");

				if (!File.Exists(abstractionsAssemblyPath))
				{
					_logger.LogError("Elastic.OpenTelemetry.OpAmp.Abstractions.dll not found at {Path}. OpAmp client will not be initialized.", abstractionsAssemblyPath);
					_client = new EmptyOpAmpClient();
					_startupTask = Task.CompletedTask;
					_isStarted = false;
					_startupFailed = true;
					return;
				}

				var loadedAssembly = loadContext.LoadFromAssemblyPath(abstractionsAssemblyPath);

				if (assembly.StartsWith("Elastic.OpenTelemetry.OpAmp.Abstractions"))
					abstractionsAssembly = loadedAssembly;
			}

			if (abstractionsAssembly is null)
			{
				_client = new EmptyOpAmpClient();
				_startupTask = Task.CompletedTask;
				_isStarted = false;
				_startupFailed = true;

				_logger.LogError("TODO");

				return;
			}

			// Get the factory type from the loaded assembly
			var factoryType = abstractionsAssembly.GetType("Elastic.OpenTelemetry.OpAmp.Abstractions.OpAmpMessageSubscriberFactory")
				?? throw new InvalidOperationException("OpAmpMessageSubscriberFactory type not found in abstractions assembly");

			// Get the Create factory method
#pragma warning disable IL2075
			var createMethod = factoryType.GetMethod("Create", 
				System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
				null,
				new[] { typeof(ILogger) },
				null)
				?? throw new InvalidOperationException("Create method not found on OpAmpMessageSubscriberFactory");
#pragma warning restore IL2075

			// Create the subscriber instance using reflection - this creates it in the isolated ALC
			var subscriberInstance = createMethod.Invoke(null, new object[] { logger })
				?? throw new InvalidOperationException("Failed to create OpAmp message subscriber");

			_logger.LogDebug("OpAmp message subscriber created in isolated ALC");

			// The subscriber implements IOpAmpMessageSubscriber which only uses primitives
			// We can call it via dynamic or reflection without type issues
			dynamic subscriber = subscriberInstance;

			// Subscribe to messages
			Action<string, byte[]> onMessageReceived = (messageType, payload) =>
			{
				_logger.LogDebug("Received OpAmp message of type: {MessageType}", messageType);
				_remoteConfigListener.HandleMessage(messageType, payload);
			};

			Action<bool> onConnectionChanged = (isConnected) =>
			{
				_logger.LogDebug("OpAmp connection status changed: {IsConnected}", isConnected);
			};

			subscriber.MessageReceived += onMessageReceived;
			subscriber.ConnectionChanged += onConnectionChanged;

			// Start the subscriber in background - subscriber manages its own lifecycle
			_ = subscriber.StartAsync(options.OpAmpEndpoint!, _startupCancellationTokenSource.Token);

			_logger.LogInformation("OpAmp client initialization started in isolated ALC");
			_client = new EmptyOpAmpClient(); // Use empty client - real client runs in isolated ALC
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Exception during isolated ALC OpAmp client initialization. Falling back to EmptyOpAmpClient. " +
				"This is likely due to a version mismatch in the OpenTelemetry AutoInstrumentation package. " +
				"Ensure the package contains matching versions of Google.Protobuf and OpenTelemetry.OpAmp.Client.");
			_client = new EmptyOpAmpClient();
			_startupTask = Task.CompletedTask;
			_isStarted = false;
			_startupFailed = true;
			return;
		}
#else
		_logger.LogDebug("Initializing OpAmp client from default context.");

		var client = new OpAmpClient(opts =>
		{
			opts.ServerUrl = new Uri(options.OpAmpEndpoint!);
			opts.ConnectionType = ConnectionType.Http;
			opts.HttpClientFactory = () =>
			{
				var client = new HttpClient();

				client.DefaultRequestHeaders.Add("User-Agent", UserAgent);

				var userHeaders = options.OpAmpHeaders!.Split(',');

				foreach (var header in userHeaders)
				{
					var parts = header.Split(['='], 2);
					if (parts.Length == 2)
					{
						var key = parts[0].Trim();
						var value = parts[1].Trim();
						client.DefaultRequestHeaders.Add(key, value);
					}
				}

				return client;
			};

			// Add custom resources to help the server identify your client.
			opts.Identification.AddIdentifyingAttribute("application.name", options.ServiceName!);

			if (!string.IsNullOrEmpty(options.ServiceVersion))
				opts.Identification.AddIdentifyingAttribute("application.version", options.ServiceVersion!);

			opts.Heartbeat.IsEnabled = false;
		});

		client.Subscribe(_remoteConfigListener);

		_client = new WrappedOpAmpClient(client);
#endif

		_startupTask = InitializeStartupAsync();
	}

#if NET8_0_OR_GREATER// && USE_ISOLATED_OPAMP_CLIENT
	[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "Dynamic code is only used when IsDynamicCodeSupported is true")]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Dynamic code is only used when IsDynamicCodeSupported is true")]
	[UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Dynamic code is only used when IsDynamicCodeSupported is true")]
	[UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Dynamic code is only used when IsDynamicCodeSupported is true")]
	private Delegate CreateConfigurationDelegate(Type optionsType, System.Reflection.Assembly opAmpAssembly, CompositeElasticOpenTelemetryOptions options)
	{
		var actionType = typeof(Action<>).MakeGenericType(optionsType);
		
		// Create a method info for a helper that takes dynamic and configures it
		var helperMethod = typeof(CentralConfiguration).GetMethod(
			nameof(ConfigureOptionsInstance),
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
		) ?? throw new InvalidOperationException("Helper method not found");

		// Create delegate: (OpAmpClientOptions opts) => ConfigureOptionsInstance((dynamic)opts, options)
		var optionsParam = System.Linq.Expressions.Expression.Parameter(optionsType, "opts");
		var callExpression = System.Linq.Expressions.Expression.Call(
			helperMethod,
			System.Linq.Expressions.Expression.Convert(optionsParam, typeof(object)),
			System.Linq.Expressions.Expression.Constant(options),
			System.Linq.Expressions.Expression.Constant(opAmpAssembly)
		);

		var lambdaExpression = System.Linq.Expressions.Expression.Lambda(actionType, callExpression, optionsParam);
		return lambdaExpression.Compile();
	}

	[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "Dynamic code is only used when IsDynamicCodeSupported is true")]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Dynamic code is only used when IsDynamicCodeSupported is true")]
	[UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Dynamic code is only used when IsDynamicCodeSupported is true")]
	private static void ConfigureOptionsInstance(object optionsObj, CompositeElasticOpenTelemetryOptions options, System.Reflection.Assembly opAmpAssembly)
	{
		var optionsType = optionsObj.GetType();

		// opts.ServerUrl = new Uri(options.OpAmpEndpoint!);
		var serverUrlProp = optionsType.GetProperty("ServerUrl", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
			?? throw new InvalidOperationException("ServerUrl property not found");
		serverUrlProp.SetValue(optionsObj, new Uri(options.OpAmpEndpoint!));

		// opts.ConnectionType = ConnectionType.Http;
		var connectionTypeProp = optionsType.GetProperty("ConnectionType", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
			?? throw new InvalidOperationException("ConnectionType property not found");
		var connectionTypeType = opAmpAssembly.GetType("OpenTelemetry.OpAmp.Client.Settings.ConnectionType")
			?? throw new InvalidOperationException("ConnectionType enum not found");
		var httpFieldValue = connectionTypeType.GetField("Http", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
			?.GetValue(null) ?? throw new InvalidOperationException("ConnectionType.Http not found");
		connectionTypeProp.SetValue(optionsObj, httpFieldValue);

		// opts.HttpClientFactory = () => { ... }
		var factoryProp = optionsType.GetProperty("HttpClientFactory", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
			?? throw new InvalidOperationException("HttpClientFactory property not found");
		
		Func<HttpClient> factory = () =>
		{
			var client = new HttpClient();
			client.DefaultRequestHeaders.Add("User-Agent", UserAgent);

			var userHeaders = options.OpAmpHeaders!.Split(',');
			foreach (var header in userHeaders)
			{
				var parts = header.Split(['='], 2);
				if (parts.Length == 2)
				{
					var key = parts[0].Trim();
					var value = parts[1].Trim();
					client.DefaultRequestHeaders.Add(key, value);
				}
			}

			return client;
		};
		factoryProp.SetValue(optionsObj, factory);

		// opts.Identification.AddIdentifyingAttribute(...)
		var identificationProp = optionsType.GetProperty("Identification", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
			?? throw new InvalidOperationException("Identification property not found");
		var identification = identificationProp.GetValue(optionsObj)
			?? throw new InvalidOperationException("Identification instance not found");

		var addAttrMethod = identification.GetType().GetMethod("AddIdentifyingAttribute", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, null, new[] { typeof(string), typeof(string) }, null)
			?? throw new InvalidOperationException("AddIdentifyingAttribute method not found");

		addAttrMethod.Invoke(identification, new object[] { "application.name", options.ServiceName! });

		if (!string.IsNullOrEmpty(options.ServiceVersion))
			addAttrMethod.Invoke(identification, new object[] { "application.version", options.ServiceVersion! });

		// opts.Heartbeat.IsEnabled = false;
		var heartbeatProp = optionsType.GetProperty("Heartbeat", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
			?? throw new InvalidOperationException("Heartbeat property not found");
		var heartbeat = heartbeatProp.GetValue(optionsObj)
			?? throw new InvalidOperationException("Heartbeat instance not found");

		var isEnabledProp = heartbeat.GetType().GetProperty("IsEnabled", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
			?? throw new InvalidOperationException("IsEnabled property not found");
		isEnabledProp.SetValue(heartbeat, false);
	}
#endif

private async Task InitializeStartupAsync()
{
	try
	{
		await _client.StartAsync(_startupCancellationTokenSource.Token).ConfigureAwait(false);
		_isStarted = true;
	}
	catch (OperationCanceledException)
	{
		_startupFailed = true;
	}
	catch (Exception ex)
	{
		_logger.LogError(ex, "OpAmp client startup failed");
		_startupFailed = true;
	}
}

internal bool IsStarted => _isStarted;

internal bool StartupFailed => _startupFailed;

internal async ValueTask WaitForStartupAsync(int timeoutMilliseconds = OpAmpBlockingStartTimeoutMilliseconds)
{
	try
	{
		using var timeoutCts = new CancellationTokenSource(timeoutMilliseconds);
		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token);

		await Task.WhenAny(
			_startupTask,
			_remoteConfigListener.FirstMessageReceivedTask
		).ConfigureAwait(false);
	}
	catch (OperationCanceledException)
	{
		_logger.LogDebug("Startup wait timeout after {TimeoutMilliseconds}ms", timeoutMilliseconds);
	}
	catch (Exception ex)
	{
		_logger.LogError(ex, "Error waiting for startup");
	}
}

internal RemoteConfigMessage? WaitForRemoteConfig(int timeoutMilliseconds = OpAmpBlockingStartTimeoutMilliseconds)
{
	try
	{
		var task = _remoteConfigListener.FirstMessageReceivedTask;
		if (task.Wait(timeoutMilliseconds))
		{
			return task.Result;
		}

		_logger.LogDebug("Timeout waiting for remote config after {TimeoutMilliseconds}ms", timeoutMilliseconds);
		return null;
	}
	catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
	{
		_logger.LogDebug("Cancelled waiting for remote config");
		return null;
	}
	catch (Exception ex)
	{
		_logger.LogError(ex, "Error waiting for remote config");
		return null;
	}
}

internal void Subscribe(ICentralConfigurationSubscriber subscriber)
{
	_subscribers.Add(subscriber);
	_logger.LogDebug("Subscriber of type '{SubscriberType}' added to Central Configuration. Total subscribers: {SubscriberCount}", subscriber.GetType().Name, _subscribers.Count);
}

public void Dispose()
{
	if (_disposed)
		return;

	_startupCancellationTokenSource.Cancel();
	_subscribers.Clear();
	_client.Dispose();
	_startupCancellationTokenSource.Dispose();
	_disposed = true;
	GC.SuppressFinalize(this);
}

public ValueTask DisposeAsync()
{
	if (_disposed)
#if NET
		return ValueTask.CompletedTask;
#else
		return new ValueTask(Task.CompletedTask);
#endif

	_startupCancellationTokenSource.Cancel();
	_subscribers.Clear();
	_client.Dispose();
	_startupCancellationTokenSource.Dispose();
	_disposed = true;
	GC.SuppressFinalize(this);

#if NET
	return ValueTask.CompletedTask;
#else
	return new ValueTask(Task.CompletedTask);
#endif
}
}
