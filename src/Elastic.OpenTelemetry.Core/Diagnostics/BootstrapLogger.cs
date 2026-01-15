// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Elastic.OpenTelemetry.Core;

namespace Elastic.OpenTelemetry.Diagnostics;

/// <summary>
/// Used to log bootstrap information before the main logger is initialized.
/// This is an experimental feature, mostly for debugging purposes but can be useful in certain support scenarios.
/// This logger writes to a file in the directory specified by the OTEL_DOTNET_AUTO_LOG_DIRECTORY environment variable.
/// It supports automatic disposal after 1 minute of inactivity to avoid unnecessary resource usage.
/// </summary>
internal static class BootstrapLogger
{
	private static StreamWriter? Writer;
	private static System.Timers.Timer? AutoCloseTimer;
	private static DateTime LastActivityUtc;

	static BootstrapLogger()
	{
		try
		{
			var logDirectory = Environment.GetEnvironmentVariable("OTEL_DOTNET_AUTO_LOG_DIRECTORY");
			var logLevel = Environment.GetEnvironmentVariable("OTEL_LOG_LEVEL");
			var enableBootstrapLogging = Environment.GetEnvironmentVariable("ELASTIC_OTEL_EXPERIMENTAL_ENABLE_BOOTSTRAP_LOGGING");

			if (string.IsNullOrEmpty(logDirectory) ||
				string.IsNullOrEmpty(logLevel) ||
				string.IsNullOrEmpty(enableBootstrapLogging) ||
				!logLevel.Equals("debug", StringComparison.OrdinalIgnoreCase) ||
				!bool.TryParse(enableBootstrapLogging, out var isEnabled) ||
				!isEnabled)
			{
				IsEnabled = false;
				return;
			}

			IsEnabled = true;

			Directory.CreateDirectory(logDirectory);

			Writer = new StreamWriter(Path.Combine(logDirectory, $"{FileLogger.FileNamePrefix}bootstrap-{FileLogger.FileNameSuffix}"), append: true) { AutoFlush = true };

			try
			{
				// This code is essentially a copy of the premable we log to the main logger.
				// As this doesn't change often and differs subtly between use cases, we duplicate it here for simplicity.
				// This might be useful in scenarios where the main logger fails to initialize.

				try
				{
					var process = Process.GetCurrentProcess();

					Writer.WriteLine("Process ID: {0}", process.Id);
					Writer.WriteLine("Process name: {0}", process.ProcessName);
					Writer.WriteLine("Process started: {0:O}", process.StartTime.ToUniversalTime());
					Writer.WriteLine("Process working set: {0} bytes", process.WorkingSet64);
					Writer.WriteLine("Thread count: {0}", process.Threads.Count);
				}
				catch
				{
					// GetCurrentProcess can throw PlatformNotSupportedException
				}

#if NET
				Writer.WriteLine("Process path: {0}", Environment.ProcessPath);
#elif NETSTANDARD
				Writer.WriteLine("Process path: {0}", "<Not available on .NET Standard>");
#elif NETFRAMEWORK
				Writer.WriteLine("Process path: {0}", "<Not available on .NET Framework>");
#endif

				Writer.WriteLine("Process architecture: {0}", RuntimeInformation.ProcessArchitecture);

				Writer.WriteLine("Current AppDomain name: {0}", AppDomain.CurrentDomain.FriendlyName);
				Writer.WriteLine("Is default AppDomain: {0}", AppDomain.CurrentDomain.IsDefaultAppDomain());

				Writer.WriteLine("Machine name: {0}", Environment.MachineName);
				Writer.WriteLine("Process username: {0}", Environment.UserName);
				Writer.WriteLine("User domain name: {0}", Environment.UserDomainName);
				Writer.WriteLine("Application base directory: {0}", AppDomain.CurrentDomain.BaseDirectory);
				Writer.WriteLine("Command current directory: {0}", Environment.CurrentDirectory);
				Writer.WriteLine("Processor count: {0}", Environment.ProcessorCount);
				Writer.WriteLine("GC is server GC: {0}", System.Runtime.GCSettings.IsServerGC);

				Writer.WriteLine("OS architecture: {0}", RuntimeInformation.OSArchitecture);
				Writer.WriteLine("OS description: {0}", RuntimeInformation.OSDescription);
				Writer.WriteLine("OS version: {0}", Environment.OSVersion);

				Writer.WriteLine(".NET framework: {0}", RuntimeInformation.FrameworkDescription);
				Writer.WriteLine("CLR version: {0}", Environment.Version);

				Writer.WriteLine("Current culture: {0}", CultureInfo.CurrentCulture.Name);
				Writer.WriteLine("Current UI culture: {0}", CultureInfo.CurrentUICulture.Name);
#if NETFRAMEWORK || NETSTANDARD2_0
				Writer.WriteLine("Dynamic code supported: {0}", true);
#else
				Writer.WriteLine("Dynamic code supported: {0}", RuntimeFeature.IsDynamicCodeSupported);
#endif
				// We don't log environment variables here as if those are wrong, we won't even get this far.

				Writer.Flush();
			}
			catch
			{
				// Swallow any exceptions to avoid impacting the application startup.
			}

			LastActivityUtc = DateTime.UtcNow;
			AutoCloseTimer = new System.Timers.Timer(60_000); // 1 minute in milliseconds
			AutoCloseTimer.Elapsed += (_, _) => CheckAndDisposeLogger();
			AutoCloseTimer.AutoReset = true;
			AutoCloseTimer.Start();

			AppDomain.CurrentDomain.ProcessExit += (_, __) => DisposeLogger();
			Console.CancelKeyPress += (_, _) => DisposeLogger();
		}
		catch
		{
			// Swallow any exceptions to avoid impacting the application startup.
			Console.Error.WriteLine("Failed to initialize BootstrapLogger.");
			IsEnabled = false;
		}
	}

	public static bool IsEnabled { get; }

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void Log(string message)
	{
		try
		{
			if (!IsEnabled || Writer is null)
				return;

			LastActivityUtc = DateTime.UtcNow;
			Writer.WriteLine($"[{DateTime.UtcNow:O}] {message}");
		}
		catch
		{
			// Swallow any exceptions to avoid impacting the application.
		}
	}

	public static void LogWithStackTrace(string message)
	{
		// We don't inline this as it will be called less frequently and the stack trace generation is more expensive.

		try
		{
			if (!IsEnabled || Writer is null)
				return;

			var stack = new StackTrace(skipFrames: 1, fNeedFileInfo: true);

			LastActivityUtc = DateTime.UtcNow;
			Writer.WriteLine($"[{DateTime.UtcNow:O}] {message}{Environment.NewLine}{stack}");
		}
		catch
		{
			// Swallow any exceptions to avoid impacting the application.
		}
	}

	public static void LogBuilderOptions<T>(BuilderOptions<T> builderOptions, string type, string method) where T : class =>
		Log($"{type}.{method} BuilderOptions:" +
				$"{Environment.NewLine}    {nameof(BuilderOptions<>.CalleeName)}: '{builderOptions.CalleeName}'" +
				$"{Environment.NewLine}    {nameof(BuilderOptions<>.SkipLogCallerInfo)}: '{builderOptions.SkipLogCallerInfo}'" +
				$"{Environment.NewLine}    {nameof(BuilderOptions<>.DeferAddOtlpExporter)}: '{builderOptions.DeferAddOtlpExporter}'" +
				$"{Environment.NewLine}    {nameof(BuilderOptions<>.UserProvidedConfigureBuilder)}: " +
				$"'{(builderOptions.UserProvidedConfigureBuilder is null ? "`null`" : "not `null`")}'");

	private static void CheckAndDisposeLogger()
	{
		try
		{
			if ((DateTime.UtcNow - LastActivityUtc).TotalMinutes >= 2)
			{
				Log("Disposing BootstrapLogger due to 1 minute of inactivity.");
				DisposeLogger();
				AutoCloseTimer?.Stop();
				AutoCloseTimer?.Dispose();
				AutoCloseTimer = null;
			}
		}
		catch
		{
			Console.Error.WriteLine("Failed to check and dispose BootstrapLogger.");
		}
	}

	private static void DisposeLogger()
	{
		Writer?.Dispose();
		Writer = null;

		AutoCloseTimer?.Stop();
		AutoCloseTimer?.Dispose();
		AutoCloseTimer = null;
	}
}
