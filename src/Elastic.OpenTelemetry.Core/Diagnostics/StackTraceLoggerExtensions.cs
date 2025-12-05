// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

#if NET8_0 || NETSTANDARD2_1
using System.Runtime.CompilerServices;
#endif

namespace Elastic.OpenTelemetry.Core.Diagnostics;

internal static class StackTraceLoggerExtensions
{
	[UnconditionalSuppressMessage("AssemblyLoadTrimming", "IL2026: RequiresUnreferencedCode", Justification = "The calls to `GetMethod`" +
		" are guarded by a RuntimeFeature.IsDynamicCodeSupported` check and therefore this method is safe to call in AoT scenarios")]
	public static void LogCallerInfo<T>(this ILogger logger, BuilderOptions<T> builderOptions) where T : class
	{
		if (!logger.IsEnabled(LogLevel.Debug) || builderOptions.CalleeName is null || builderOptions.SkipLogCallerInfo)
			return;

		var calleeName = builderOptions.CalleeName;

		try
		{
#if NET8_0 || NETSTANDARD2_1
			// For now, we skip this log line entirely for AOT
			// TODO: We should be able to provide some fallback and/or even use GetMethod safely in this scenario
			// We need to test these scenarios and enhance in a future PR.
			if (!RuntimeFeature.IsDynamicCodeSupported)
				return;
#endif
			var stackTrace = new StackTrace(skipFrames: 1, fNeedFileInfo: true);

			if (stackTrace is null)
				return;

			foreach (var frame in stackTrace.GetFrames() ?? [])
			{
#if NETFRAMEWORK || NETSTANDARD2_0 || NET8_0 || NETSTANDARD2_1
				var caller = $"{frame.GetType().AssemblyQualifiedName}.{frame.GetMethod()}";

				var method = frame.GetMethod();
				var declaringAssemblyName = method?.DeclaringType?.Assembly?.GetName().FullName;

				if (method is null ||
					declaringAssemblyName is null ||
					declaringAssemblyName.StartsWith("Elastic", StringComparison.Ordinal) ||
					declaringAssemblyName.StartsWith("OpenTelemetry", StringComparison.Ordinal))
					continue;

				var file = frame.GetFileName() ?? "<unknown>";
				var line = frame.GetFileLineNumber();

				if (method.DeclaringType?.FullName is not null)
				{
					if (line > 0)
					{
						logger.LogDebug("{Callee} invoked by {DeclaringType}.{MethodName} in {DeclaringAssembly} at {File}:{Line}",
							calleeName, method.DeclaringType.FullName, method.Name, declaringAssemblyName, file, line);
					}
					else
					{
						logger.LogDebug("{Callee} invoked by {DeclaringType}.{MethodName} in {DeclaringAssembly}",
						calleeName, method.DeclaringType.FullName, method.Name, declaringAssemblyName);
					}
				}
				else
				{
					if (line > 0)
					{
						logger.LogDebug("{Callee} invoked by {MethodName} in {DeclaringAssembly} at {File}:{Line}",
						calleeName, method.Name, declaringAssemblyName, file, line);
					}
					else
					{
						logger.LogDebug("{Callee} invoked by {MethodName} in {DeclaringAssembly}",
							calleeName, method.Name, declaringAssemblyName);
					}
				}
				break;
#elif NET9_0_OR_GREATER
				var method = DiagnosticMethodInfo.Create(frame);

				if (method is null ||
					method.DeclaringAssemblyName.StartsWith("Elastic", StringComparison.Ordinal) ||
					method.DeclaringAssemblyName.StartsWith("OpenTelemetry", StringComparison.Ordinal))
					continue;

				var file = frame.GetFileName() ?? "<unknown>";
				var line = frame.GetFileLineNumber();

				if (line > 0)
				{
					logger.LogDebug("{Callee} invoked by {DeclaringType}.{MethodName} in {DeclaringAssembly} at {File}:{Line}",
						calleeName, method.DeclaringTypeName, method.Name, method.DeclaringAssemblyName, file, line);
				}
				else
				{
					logger.LogDebug("{Callee} invoked by {DeclaringType}.{MethodName} in {DeclaringAssembly}",
						calleeName, method.DeclaringTypeName, method.Name, method.DeclaringAssemblyName);
				}

				break;
#endif
			}
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Unable to log caller info.");
		}
	}
}
