// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information
using System.Reflection;
using Microsoft.Extensions.Logging;

using static Elastic.OpenTelemetry.Diagnostics.ElasticOpenTelemetryDiagnosticSource;

namespace Elastic.OpenTelemetry;

/// <summary>
/// Supports building and accessing an <see cref="IAgent"/> which collects and ships observability signals.
/// </summary>
public static partial class Agent
{
	private static readonly object Lock = new();
	private static IAgent? CurrentAgent;

	static Agent()
	{
		var assemblyInformationalVersion = typeof(Agent).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
		InformationalVersion = ParseAssemblyInformationalVersion(assemblyInformationalVersion);
	}

	/// <summary>
	/// Returns the singleton <see cref="IAgent"/> instance.
	/// </summary>
	/// <remarks>
	/// If an instance is not already initialized, this will create and return a
	/// default <see cref="IAgent"/> configured with recommended Elastic defaults.
	/// </remarks>
	public static IAgent Current
	{
		get
		{
			if (CurrentAgent is not null)
				return CurrentAgent;

			lock (Lock)
			{
				// disable to satisfy double check lock pattern analyzer
				// ReSharper disable once InvertIf
				if (CurrentAgent is null)
				{
					var agent = new AgentBuilder().Build();
					CurrentAgent = agent;
				}
				return CurrentAgent;
			}
		}
	}

	internal static string InformationalVersion { get; }

	/// <summary>
	/// Builds an <see cref="IAgent"/>.
	/// </summary>
	/// <returns>An <see cref="IAgent"/> instance.</returns>
	/// <exception cref="Exception">
	/// An exception will be thrown if <see cref="Build"/>
	/// is called more than once during the lifetime of an application.
	/// </exception>
	public static IAgent Build(Action<AgentBuilder>? configuration = null)
	{
		CheckCurrent();

		lock (Lock)
		{
			CheckCurrent();
			var agentBuilder = new AgentBuilder();
			configuration?.Invoke(agentBuilder);
			var agent = agentBuilder.Build();
			CurrentAgent = agent;
			return CurrentAgent;
		}

		static void CheckCurrent()
		{
			if (CurrentAgent is not null)
			{
				Log(AgentBuildCalledMultipleTimesEvent);
				throw new Exception();
			}
		}
	}

	internal const string BuildErrorMessage = $"{nameof(Agent)}.{nameof(Build)} called twice or after " +
		$"{nameof(Agent)}.{nameof(Current)} was accessed.";

	internal const string SetAgentErrorMessage = $"{nameof(Agent)}.{nameof(SetAgent)} called twice" +
		$"or after {nameof(Agent)}.{nameof(Build)} or after {nameof(Agent)}.{nameof(Current)} was accessed.";

	[LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = SetAgentErrorMessage)]
	internal static partial void SetAgentError(this ILogger logger);

	/// <summary>
	/// TODO
	/// </summary>
	/// <param name="agent"></param>
	/// <param name="logger"></param>
	/// <returns></returns>
	/// <exception cref="Exception"></exception>
	internal static IAgent SetAgent(IAgent agent, ILogger logger)
	{
		CheckCurrent(logger);

		lock (Lock)
		{
			CheckCurrent(logger);
			logger.LogInformation($"Setting {nameof(CurrentAgent)}.");
			CurrentAgent = agent;
			return CurrentAgent;
		}

		static void CheckCurrent(ILogger logger)
		{
			if (CurrentAgent is not null)
			{
				Log(AgentSetAgentCalledMultipleTimesEvent);
				logger.SetAgentError();
				throw new Exception(SetAgentErrorMessage);
			}
		}
	}

	internal static string ParseAssemblyInformationalVersion(string? informationalVersion)
	{
		if (string.IsNullOrWhiteSpace(informationalVersion))
		{
			informationalVersion = "1.0.0";
		}

		/*
         * InformationalVersion will be in the following format:
         *   {majorVersion}.{minorVersion}.{patchVersion}.{pre-release label}.{pre-release version}.{gitHeight}+{Git SHA of current commit}
         * Ex: 1.5.0-alpha.1.40+807f703e1b4d9874a92bd86d9f2d4ebe5b5d52e4
         * The following parts are optional: pre-release label, pre-release version, git height, Git SHA of current commit
         */

		var indexOfPlusSign = informationalVersion!.IndexOf('+');
		return indexOfPlusSign > 0
			? informationalVersion[..indexOfPlusSign]
			: informationalVersion;
	}
}
