// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

#if NET8_0_OR_GREATER// && USE_ISOLATED_OPAMP_CLIENT

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.Core.Configuration;

//internal sealed class OpAmpSubscriberBridge : DispatchProxy
//{
//	private Action<byte[]> _onMessage;
//	private Action<bool> _onConnectionChange;
//	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]
//	private Type _messageType;
//	private MethodInfo _toByteArrayMethod;

//	// Called by OpAmpManager to set up the bridge
//	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Dynamic invocations are only used when IsDynamicCodeSupported is true")]
//	public void Initialize(
//		Type subscriberInterface,
//		Action<byte[]> onMessage,
//		Action<bool> onConnectionChange)
//	{
//		_onMessage = onMessage;
//		_onConnectionChange = onConnectionChange;

//		// Find the message type for conversion
//		var assembly = subscriberInterface.Assembly;
//		_messageType = assembly!.GetType("OpenTelemetry.OpAmp.Client.OpAmpMessage")!;
//		_toByteArrayMethod = _messageType.GetMethod("ToByteArray")!;
//	}

//	// This is called when OpAmp invokes interface methods
//	protected override object Invoke(MethodInfo targetMethod, object[] args)
//	{
//		try
//		{
//			switch (targetMethod.Name)
//			{
//				case "OnMessage":
//				case "OnMessageReceived":
//					HandleMessage(args[0]);
//					break;

//				case "OnConnectionStatusChanged":
//				case "OnConnectionChanged":
//					HandleConnectionChange(args[0]);
//					break;

//				default:
//					Console.WriteLine($"[Bridge] Unhandled method: {targetMethod.Name}");
//					break;
//			}
//		}
//		catch (Exception ex)
//		{
//			Console.WriteLine($"[Bridge] Error in {targetMethod.Name}: {ex.Message}");
//		}

//		return null;
//	}

//	private void HandleMessage(object message)
//	{
//		try
//		{
//			// Convert the message object to bytes
//			byte[] bytes = null;

//			if (_toByteArrayMethod != null)
//			{
//				// If message has ToByteArray method
//				bytes = (byte[])_toByteArrayMethod.Invoke(message, null);
//			}
//			else if (message is byte[] directBytes)
//			{
//				// If it's already bytes
//				bytes = directBytes;
//			}
//			else
//			{
//				// Try to serialize using any available method
//				var msgType = message.GetType();
//				var serializeMethod = msgType.GetMethod("ToByteArray")
//					?? msgType.GetMethod("Serialize");

//				if (serializeMethod != null)
//				{
//					bytes = (byte[])serializeMethod.Invoke(message, null);
//				}
//			}

//			if (bytes != null)
//			{
//				// Call back to instrumentation code (crosses ALC boundary)
//				_onMessage?.Invoke(bytes);
//			}
//		}
//		catch (Exception ex)
//		{
//			Console.WriteLine($"[Bridge] Error converting message: {ex.Message}");
//		}
//	}

//	private void HandleConnectionChange(object status)
//	{
//		try
//		{
//			bool isConnected = false;

//			// Handle different possible status types
//			if (status is bool boolStatus)
//			{
//				isConnected = boolStatus;
//			}
//			else if (status is Enum enumStatus)
//			{
//				// Assume "Connected" or similar enum value means connected
//				var statusString = enumStatus.ToString();
//				isConnected = statusString.Contains("Connected", StringComparison.OrdinalIgnoreCase);
//			}
//			else if (status is string strStatus)
//			{
//				isConnected = strStatus.Contains("Connected", StringComparison.OrdinalIgnoreCase);
//			}

//			_onConnectionChange?.Invoke(isConnected);
//		}
//		catch (Exception ex)
//		{
//			Console.WriteLine($"[Bridge] Error handling connection change: {ex.Message}");
//		}
//	}

//	public static object Create(Type interfaceType)
//	{
//		return Create(interfaceType, typeof(OpAmpSubscriberBridge));
//	}
//}

//internal sealed class OpAmpManager : IDisposable
//{
//	private readonly OpAmpLoadContext _loadContext;

//	private readonly object _clientInstance;
//	private object _subscriberInstance;

//	private readonly MethodInfo _startMethod;
//	private readonly MethodInfo _stopMethod;

//	public OpAmpManager(ILogger logger)
//	{
//		// Create isolated load context
//		_loadContext = new OpAmpLoadContext(logger);

//		// Load OpAmp assembly in isolated context
//		var opAmpAssembly = _loadContext.LoadFromAssemblyPath(opAmpClientPath);

//		// Find the client type
//		var clientType = opAmpAssembly.GetType("OpenTelemetry.OpAmp.Client.OpAmpClient")
//			?? throw new InvalidOperationException("OpAmpClient type not found");

//		// Create client instance
//		_clientInstance = Activator.CreateInstance(clientType);

//		// Cache methods for later use
//		_startMethod = clientType.GetMethod("Start") ?? clientType.GetMethod("Connect");
//		_stopMethod = clientType.GetMethod("Stop") ?? clientType.GetMethod("Disconnect");
//	}

//	public void Start(
//		string endpoint,
//		Action<byte[]> onMessageReceived,
//		Action<bool> onConnectionChanged)
//	{
//		// Create subscriber in isolated context that bridges to our callbacks
//		_subscriberInstance = CreateBridgeSubscriber(
//			_loadContext,
//			_clientInstance,
//			onMessageReceived,
//			onConnectionChanged
//		);

//		// Start the client
//		_startMethod?.Invoke(_clientInstance, new object[] { endpoint });
//	}

//	private object CreateBridgeSubscriber(
//		OpAmpLoadContext context,
//		object client,
//		Action<byte[]> onMessage,
//		Action<bool> onConnectionChange)
//	{
//		// Get subscriber interface type from isolated context
//		var opAmpAssembly = client.GetType().Assembly;
//		var subscriberInterface = opAmpAssembly.GetType("OpenTelemetry.OpAmp.Client.IOpAmpSubscriber");

//		if (subscriberInterface == null)
//		{
//			throw new InvalidOperationException("IOpAmpSubscriber interface not found");
//		}

//		// Create the bridge subscriber using helper class
//		var bridgeType = typeof(OpAmpSubscriberBridge);
//		var bridge = Activator.CreateInstance(bridgeType);

//		// Initialize it with callbacks
//		var initMethod = bridgeType.GetMethod("Initialize");
//		initMethod.Invoke(bridge, new object[] { subscriberInterface, onMessage, onConnectionChange });

//		// Subscribe to the client
//		var subscribeMethod = client.GetType().GetMethod("Subscribe");
//		subscribeMethod?.Invoke(client, new[] { bridge });

//		return bridge;
//	}

//	public void Dispose()
//	{
//		try
//		{
//			_stopMethod?.Invoke(_clientInstance, null);
//		}
//		catch { }

//		_loadContext?.Unload();
//	}
//}

/// <summary>
/// Custom AssemblyLoadContext for isolating Google.Protobuf and OpenTelemetry.OpAmp.Client
/// to prevent version conflicts when the instrumented application brings its own versions
/// of these dependencies.
/// 
/// This is used in profiler-loaded scenarios (AutoInstrumentation) where we have zero control 
/// over the application's dependency versions.
/// 
/// Once an assembly is loaded in this context, its dependencies are resolved within this
/// context as well, preventing version conflicts with the default ALC.
///
/// Note: This class is only compiled for .NET 8 and newer frameworks that support AssemblyLoadContext.
/// </summary>
internal sealed class OpAmpLoadContext : AssemblyLoadContext
{
	private readonly ILogger _logger;
	private readonly AssemblyDependencyResolver? _resolver = null;

	public OpAmpLoadContext(ILogger logger) : base("ElasticOpenTelemetryIsolatedOpAmp", isCollectible: false)
	{
		_logger = logger;

		var otelInstallationPath = Environment.GetEnvironmentVariable("OTEL_DOTNET_AUTO_INSTALL_DIR");

		if (string.IsNullOrEmpty(otelInstallationPath))
		{
			_logger.LogWarning("OpAmpLoadContext: OTEL_DOTNET_AUTO_INSTALL_DIR environment variable is not set. " +
				"Falling back to default assembly resolution which may lead to version conflicts.");

			return;
		}

		OtelInstallationPath = Path.Join(otelInstallationPath, "net");

		// TODO - Check path exists

		_logger.LogDebug("OpAmpLoadContext: Initializing isolated load context for OpenTelemetry OpAmp dependencies for '{OtelInstallationPath}'",
			otelInstallationPath ?? "<null>");

		_resolver = new AssemblyDependencyResolver(Path.Join(OtelInstallationPath, "Elastic.OpenTelemetry.AutoInstrumentation.dll"));
		
		// Hook into AssemblyResolve to handle version mismatches
		Resolving += OnAssemblyResolve;
	}

	public string? OtelInstallationPath { get; }

	private Assembly? OnAssemblyResolve(AssemblyLoadContext context, AssemblyName assemblyName)
	{
		if (assemblyName.Name is not "Google.Protobuf" and not "OpenTelemetry.OpAmp.Client")
		{
			return null;
		}

		_logger.LogDebug("OpAmpLoadContext.OnAssemblyResolve() called for: '{AssemblyName}' version {Version}",
			assemblyName.Name, assemblyName.Version);

		// Try to load from the resolver first
		if (_resolver is not null)
		{
			try
			{
				var resolvedPath = _resolver.ResolveAssemblyToPath(assemblyName);
				if (resolvedPath is not null && File.Exists(resolvedPath))
				{
					_logger.LogDebug("OpAmpLoadContext.OnAssemblyResolve: Resolver found '{AssemblyName}' at {ResolvedPath}",
						assemblyName.Name, resolvedPath);
#pragma warning disable IL2026
					return LoadFromAssemblyPath(resolvedPath);
#pragma warning restore IL2026
				}
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "OpAmpLoadContext.OnAssemblyResolve: Resolver failed for '{AssemblyName}'", assemblyName.Name);
			}
		}

		// Fallback: try direct path resolution
		if (!string.IsNullOrEmpty(OtelInstallationPath))
		{
			var assemblyPath = Path.Combine(OtelInstallationPath, $"{assemblyName.Name}.dll");
			if (File.Exists(assemblyPath))
			{
				try
				{
					_logger.LogDebug("OpAmpLoadContext.OnAssemblyResolve: Loading '{AssemblyName}' from direct path: {AssemblyPath}",
						assemblyName.Name, assemblyPath);
#pragma warning disable IL2026
					return LoadFromAssemblyPath(assemblyPath);
#pragma warning restore IL2026
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "OpAmpLoadContext.OnAssemblyResolve: Failed to load '{AssemblyName}' from {AssemblyPath}",
						assemblyName.Name, assemblyPath);
				}
			}
		}

		_logger.LogDebug("OpAmpLoadContext.OnAssemblyResolve: Could not resolve '{AssemblyName}' version {Version}",
			assemblyName.Name, assemblyName.Version);

		return null;
	}

	[UnconditionalSuppressMessage("AssemblyLoadTrimming", "IL2026: RequiresUnreferencedCode", Justification = "The calls to this ALC will be guarded by a runtime check")]
	protected override Assembly? Load(AssemblyName assemblyName)
	{
		_logger.LogDebug("OpAmpLoadContext.Load() called for assembly: '{AssemblyName}' version {Version}", 
			assemblyName.Name, assemblyName.Version);

		// Only intercept known problematic assemblies that need isolation
		if (assemblyName.Name is not "Google.Protobuf" and not "OpenTelemetry.OpAmp.Client")
		{
			_logger.LogDebug("OpAmpLoadContext: Assembly '{AssemblyName}' is not targeted for isolation. Falling back to default resolution.",
				assemblyName.Name);

			return null;
		}

		if (_resolver is null)
		{
			_logger.LogDebug("OpAmpLoadContext: Resolver is not initialized for '{AssemblyName}'. Attempting direct path resolution.",
				assemblyName.Name);

			// Try direct path resolution if resolver is not initialized
			if (!string.IsNullOrEmpty(OtelInstallationPath))
			{
				var assemblyPath = Path.Combine(OtelInstallationPath, $"{assemblyName.Name}.dll");
				if (File.Exists(assemblyPath))
				{
					try
					{
						_logger.LogDebug("OpAmpLoadContext: Loading '{AssemblyName}' from path: {AssemblyPath}", 
							assemblyName.Name, assemblyPath);
#pragma warning disable IL2026
						return LoadFromAssemblyPath(assemblyPath);
#pragma warning restore IL2026
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "OpAmpLoadContext: Failed to load '{AssemblyName}' from {AssemblyPath}", 
							assemblyName.Name, assemblyPath);
					}
				}
			}

			return null;
		}

		var resolvedPath = _resolver.ResolveAssemblyToPath(assemblyName);

		if (resolvedPath is not null)
		{
			_logger.LogDebug("OpAmpLoadContext: Resolver resolved '{AssemblyName}' to path: {ResolvedPath}",
				assemblyName.Name, resolvedPath);

			try
			{
				return LoadFromAssemblyPath(resolvedPath);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "OpAmpLoadContext: Failed to load '{AssemblyName}' from resolved path: {ResolvedPath}", 
					assemblyName.Name, resolvedPath);
				throw;
			}
		}
		else
		{
			_logger.LogWarning("OpAmpLoadContext: Resolver could not resolve '{AssemblyName}' version {Version}. Falling back to default resolution.",
				assemblyName.Name, assemblyName.Version);
		}

		return null;
	}

	//private static IsolatedOpAmpLoadContext? _instance;
	//private static readonly object LockObject = new();
	
	//private readonly string _basePath;

	//private IsolatedOpAmpLoadContext(string basePath) : base("ElasticOpenTelemetryIsolatedOpAmp", isCollectible: false) => _basePath = basePath;

	///// <summary>
	///// Gets or creates the singleton isolated load context.
	///// </summary>
	//public static IsolatedOpAmpLoadContext GetOrCreate(string? basePath = null)
	//{
	//	if (_instance != null)
	//		return _instance;

	//	lock (LockObject)
	//	{
	//		if (_instance != null)
	//			return _instance;

	//		basePath ??= AppContext.BaseDirectory;

	//		_instance = new IsolatedOpAmpLoadContext(basePath);
	//		return _instance;
	//	}
	//}

	//protected override Assembly? Load(AssemblyName assemblyName)
	//{
	//	// Only intercept known problematic assemblies that need isolation
	//	if (assemblyName.Name is "Google.Protobuf" or "OpenTelemetry.OpAmp.Client")
	//	{
	//		var assemblyPath = Path.Combine(_basePath, $"{assemblyName.Name}.dll");
			
	//		if (File.Exists(assemblyPath))
	//		{
	//			try
	//			{
	//				return LoadFromAssemblyPath(assemblyPath);
	//			}
	//			catch (Exception ex)
	//			{
	//				System.Diagnostics.Debug.WriteLine(
	//					$"IsolatedOpAmpLoadContext: Failed to load {assemblyName.Name} from {assemblyPath}: {ex.Message}");
	//			}
	//		}
	//	}

	//	// Let default resolution handle other assemblies
	//	return null;
	//}
}

#endif
