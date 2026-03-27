// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.OpAmp;

/// <summary>
/// Logger messages for <c>OpAmpIsolatedLoadContext</c>. Unguarded by <c>#if</c> so that
/// EventIds 150-156 are always compiled and testable from the snapshot test.
/// The callers in <c>OpAmpIsolatedLoadContext.cs</c> remain behind
/// <c>#if NET &amp;&amp; USE_ISOLATED_OPAMP_CLIENT</c>.
/// </summary>
internal static partial class OpAmpIsolatedLoadContextLoggerMessages
{
	// EventIds 150-159 reserved for OpAmpIsolatedLoadContext

	[LoggerMessage(EventId = 150, EventName = "AssemblyResolved", Level = LogLevel.Debug,
		Message = "{ClassName}.{MethodName}: Resolved {AssemblyName} to {Path}")]
	internal static partial void LogAssemblyResolved(this ILogger logger, string className, string methodName, string assemblyName, string path);

	[LoggerMessage(EventId = 151, EventName = "AssemblyResolutionFailed", Level = LogLevel.Warning,
		Message = "{ClassName}.{MethodName}: Failed to resolve assembly {AssemblyName}")]
	internal static partial void LogAssemblyResolutionFailed(this ILogger logger, Exception exception, string className, string methodName, string assemblyName);

	[LoggerMessage(EventId = 152, EventName = "FactoryTypeNotFound", Level = LogLevel.Error,
		Message = "{ClassName}.{MethodName}: Could not locate factory type `{TypeName}`")]
	internal static partial void LogFactoryTypeNotFound(this ILogger logger, string className, string methodName, string typeName);

	[LoggerMessage(EventId = 153, EventName = "FactoryActivationFailed", Level = LogLevel.Error,
		Message = "{ClassName}.{MethodName}: Failed to activate factory `{TypeName}`")]
	internal static partial void LogFactoryActivationFailed(this ILogger logger, string className, string methodName, string typeName);

	[LoggerMessage(EventId = 154, EventName = "FactoryCreateException", Level = LogLevel.Error,
		Message = "{ClassName}.{MethodName}: Exception creating OpAmp client via factory `{TypeName}`")]
	internal static partial void LogFactoryCreateException(this ILogger logger, Exception exception, string className, string methodName, string typeName);

	[LoggerMessage(EventId = 155, EventName = "AssemblyResolverReturnedNull", Level = LogLevel.Warning,
		Message = "{ClassName}.{MethodName}: Resolver returned null for whitelisted assembly " +
			"{AssemblyName}. The assembly may not be listed in the component's .deps.json.")]
	internal static partial void LogAssemblyResolverReturnedNull(
		this ILogger logger, string className, string methodName, string assemblyName);

	[LoggerMessage(EventId = 156, EventName = "AssemblyResolvedPathNotFound", Level = LogLevel.Warning,
		Message = "{ClassName}.{MethodName}: Resolver returned path {Path} for whitelisted assembly " +
			"{AssemblyName}, but the file does not exist on disk.")]
	internal static partial void LogAssemblyResolvedPathNotFound(
		this ILogger logger, string className, string methodName, string assemblyName, string path);
}
