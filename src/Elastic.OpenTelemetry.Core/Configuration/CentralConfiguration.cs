// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenTelemetry.OpAmp.Client;
using OpenTelemetry.OpAmp.Client.Messages;
using OpenTelemetry.OpAmp.Client.Settings;

#if NETFRAMEWORK
using System.Net.Http;
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
		if (options.IsOpAmpEnabled() is false)
		{
			// TODO
		}

		_logger = logger;
		_startupCancellationTokenSource = new CancellationTokenSource();
		_remoteConfigListener = new RemoteConfigMessageListener();

#if NET8_0_OR_GREATER //&& USE_ISOLATED_OPAMP_CLIENT

		if (!RuntimeFeature.IsDynamicCodeSupported)
		{
			_client = new EmptyOpAmpClient();
			_startupTask = Task.CompletedTask;
			_isStarted = false;
			_startupFailed = true;

			_logger.LogError("Dynamic code is not supported in the current runtime. OpAmp client will not be initialized.");

			return;
		}

		// Initialize isolated loading of OpAmp dependencies to prevent version conflicts
		_logger.LogDebug("Initializing OpAmp client in isolated load context to prevent dependency version conflicts.");

		var loadContext = new OpAmpLoadContext(logger);

		var opAmpAssembly = loadContext.LoadFromAssemblyName(new System.Reflection.AssemblyName("OpenTelemetry.OpAmp.Client"));

		if (opAmpAssembly is null)
		{
			_client = new EmptyOpAmpClient();
			_startupTask = Task.CompletedTask;
			_isStarted = false;
			_startupFailed = true;

			_logger.LogError("Failed to load OpenTelemetry.OpAmp.Client assembly in isolated load context. OpAmp client will not be initialized.");

			return;
		}
		else
		{
			var opAmpClientType = opAmpAssembly.GetType("OpenTelemetry.OpAmp.Client.OpAmpClient");
			if (opAmpClientType is null)
			{
				_logger.LogError("Failed to get OpAmpClient type from OpenTelemetry.OpAmp.Client assembly.");
			}
			else
			{
				var client = (OpAmpClient)Activator.CreateInstance(opAmpClientType, new object[]
				{
					new Action<OpAmpClientOptions>(opts =>
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
					})
				})!;
				client.Subscribe(_remoteConfigListener);

				_client = new WrappedOpAmpClient(client);
			}
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
