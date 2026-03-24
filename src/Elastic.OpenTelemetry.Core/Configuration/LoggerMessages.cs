// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.Core.Configuration;

internal static partial class LoggerMessages
{
	// NOTES:
	// - The IDs and EventNames should ideally not change to ensure consistency in log querying.
	// - EventIds start at 100 to avoid conflicts with Core Diagnostics LoggerMessages (1-60).
	//   OpAmp LoggerMessages start at 200.

	// CentralConfiguration messages

	[LoggerMessage(EventId = 100, EventName = "OpAmpNotEnabled", Level = LogLevel.Debug,
		Message = "{ClassName}: OpAMP is not enabled in the provided options. Central Configuration will not be initialized")]
	internal static partial void LogOpAmpNotEnabled(this ILogger logger, string className);

	[LoggerMessage(EventId = 101, EventName = "InitializingCentralConfig", Level = LogLevel.Information,
		Message = "{ClassName}: Initializing Central Configuration with OpAMP endpoint: {OpAmpEndpoint}")]
	internal static partial void LogInitializingCentralConfig(this ILogger logger, string className, string? opAmpEndpoint);

	[LoggerMessage(EventId = 102, EventName = "UsingIsolatedLoadContext", Level = LogLevel.Debug,
		Message = "{ClassName}: Runtime supports dynamic code generation. Using isolated load context for OpAMP client implementation.")]
	internal static partial void LogUsingIsolatedLoadContext(this ILogger logger, string className);

	[LoggerMessage(EventId = 103, EventName = "DynamicCodeNotSupported", Level = LogLevel.Warning,
		Message = "{ClassName}: Runtime does not support dynamic code generation. Unable to use isolated load context for OpAMP client implementation.")]
	internal static partial void LogDynamicCodeNotSupported(this ILogger logger, string className);

	[LoggerMessage(EventId = 104, EventName = "OpAmpNotSupportedOnPlatform", Level = LogLevel.Warning,
		Message = "{ClassName}: OpAMP client implementation is not supported on this platform. Central Configuration will not be initialized.")]
	internal static partial void LogOpAmpNotSupportedOnPlatform(this ILogger logger, string className);

	[LoggerMessage(EventId = 105, EventName = "UsingEmptyOpAmpClient", Level = LogLevel.Debug,
		Message = "{ClassName}: Using empty OpAMP client implementation.")]
	internal static partial void LogUsingEmptyOpAmpClient(this ILogger logger, string className);

	[LoggerMessage(EventId = 106, EventName = "OpAmpClientCreated", Level = LogLevel.Debug,
		Message = "{ClassName}: Successfully created OpAMP client instance.")]
	internal static partial void LogOpAmpClientCreated(this ILogger logger, string className);

	[LoggerMessage(EventId = 107, EventName = "OpAmpClientStarted", Level = LogLevel.Information,
		Message = "{ClassName}: OpAMP client started in {ElapsedMs} ms.")]
	internal static partial void LogOpAmpClientStarted(this ILogger logger, string className, double elapsedMs);

	[LoggerMessage(EventId = 108, EventName = "OpAmpClientStartTimeout", Level = LogLevel.Warning,
		Message = "{ClassName}: OpAMP client failed to start within the timeout period of {TimeoutMs} ms. Proceeding without central configuration.")]
	internal static partial void LogOpAmpClientStartTimeout(this ILogger logger, string className, int timeoutMs);

	[LoggerMessage(EventId = 109, EventName = "DisposingOpAmpClient", Level = LogLevel.Debug,
		Message = "{ClassName}: Disposing OpAMP client.")]
	internal static partial void LogDisposingOpAmpClient(this ILogger logger, string className);

	[LoggerMessage(EventId = 110, EventName = "AsyncDisposingOpAmpClient", Level = LogLevel.Debug,
		Message = "{ClassName}: Async disposing OpAMP client.")]
	internal static partial void LogAsyncDisposingOpAmpClient(this ILogger logger, string className);

	[LoggerMessage(EventId = 111, EventName = "StopAsyncTimedOut", Level = LogLevel.Warning,
		Message = "{ClassName}: StopAsync timed out during async dispose.")]
	internal static partial void LogStopAsyncTimedOut(this ILogger logger, string className);

	[LoggerMessage(EventId = 112, EventName = "StopAsyncFaulted", Level = LogLevel.Warning,
		Message = "{ClassName}: StopAsync faulted during async dispose: {ExceptionType}")]
	internal static partial void LogStopAsyncFaulted(this ILogger logger, string className, string exceptionType);

	[LoggerMessage(EventId = 113, EventName = "OperationTimedOut", Level = LogLevel.Warning,
		Message = "{ClassName}: {OperationName} did not complete within {TimeoutMs} ms.")]
	internal static partial void LogOperationTimedOut(this ILogger logger, string className, string operationName, int timeoutMs);

	[LoggerMessage(EventId = 114, EventName = "OperationFaulted", Level = LogLevel.Warning,
		Message = "{ClassName}: {OperationName} faulted: {ExceptionType}")]
	internal static partial void LogOperationFaulted(this ILogger logger, string className, string operationName, string exceptionType);

	[LoggerMessage(EventId = 115, EventName = "DisposeFaulted", Level = LogLevel.Warning,
		Message = "{ClassName}: Dispose faulted: {ExceptionType}")]
	internal static partial void LogDisposeFaulted(this ILogger logger, string className, string exceptionType);

	[LoggerMessage(EventId = 116, EventName = "OpAmpClientCreationFailed", Level = LogLevel.Error,
		Message = "{ClassName}: Failed to create OpAMP client: {ExceptionType}")]
	internal static partial void LogOpAmpClientCreationFailed(this ILogger logger, Exception exception, string className, string exceptionType);

	// RemoteConfigSubscriber messages

	[LoggerMessage(EventId = 130, EventName = "ReceivedRemoteConfigMessage", Level = LogLevel.Debug,
		Message = "{ClassName}.{MethodName}: Received remote config message")]
	internal static partial void LogReceivedRemoteConfigMessage(this ILogger logger, string className, string methodName);

	[LoggerMessage(EventId = 131, EventName = "ReceivedInitialCentralConfig", Level = LogLevel.Debug,
		Message = "{ClassName}.{MethodName}: Received initial central configuration")]
	internal static partial void LogReceivedInitialCentralConfig(this ILogger logger, string className, string methodName);
}
