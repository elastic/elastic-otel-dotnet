// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections;
using Elastic.OpenTelemetry.Configuration;
using Microsoft.Extensions.Logging;
using static Elastic.OpenTelemetry.Configuration.EnvironmentVariables;

namespace Elastic.OpenTelemetry.Tests.Configuration;

public class GlobalLogConfigurationTests
{
	[Fact]
	public void Check_Defaults()
	{
		var config = new ElasticOpenTelemetryOptions(new Hashtable());
		config.GlobalLogEnabled.Should().BeFalse();
		config.LogLevel.Should().Be(LogLevel.Warning);
		config.LogDirectory.Should().Be(config.LogDirectoryDefault);
		config.LogTargets.Should().Be(LogTargets.None);
	}

	//
	[Theory]
	[InlineData(OTEL_LOG_LEVEL, "Info")]
	[InlineData(OTEL_DOTNET_AUTO_LOG_DIRECTORY, "1")]
	//only if explicitly specified to 'none' should we not default to file logging.
	[InlineData(ELASTIC_OTEL_LOG_TARGETS, "file")]
	public void CheckActivation(string environmentVariable, string value)
	{
		var config = new ElasticOpenTelemetryOptions(new Hashtable { { environmentVariable, value } });
		config.GlobalLogEnabled.Should().BeTrue();
		config.LogTargets.Should().Be(LogTargets.File);
	}

	//
	[Theory]
	[InlineData(OTEL_LOG_LEVEL, "none")]
	//only if explicitly specified to 'none' should we not default to file logging.
	[InlineData(ELASTIC_OTEL_LOG_TARGETS, "none")]
	public void CheckDeactivation(string environmentVariable, string value)
	{
		var config = new ElasticOpenTelemetryOptions(new Hashtable
		{
			{ OTEL_DOTNET_AUTO_LOG_DIRECTORY, "" },
			{ environmentVariable, value }
		});
		config.GlobalLogEnabled.Should().BeFalse();
		config.LogTargets.Should().Be(LogTargets.None);
	}

	[Theory]
	//only specifying apm_log_level not sufficient, needs explicit directory configuration
	//setting targets to none will result in no global trace logging
	[InlineData(ELASTIC_OTEL_LOG_TARGETS, "None")]
	//setting file log level to none will result in no global trace logging
	[InlineData(OTEL_LOG_LEVEL, "None")]
	public void CheckNonActivation(string environmentVariable, string value)
	{
		var config = new ElasticOpenTelemetryOptions(new Hashtable { { environmentVariable, value } });
		config.GlobalLogEnabled.Should().BeFalse();
	}

	[Theory]
	[InlineData("trace", LogLevel.Debug, true)]
	[InlineData("Trace", LogLevel.Debug, true)]
	[InlineData("TraCe", LogLevel.Debug, true)]
	[InlineData("debug", LogLevel.Debug, true)]
	[InlineData("info", LogLevel.Information, false)]
	[InlineData("warn", LogLevel.Warning, false)]
	[InlineData("error", LogLevel.Error, false)]
	[InlineData("none", LogLevel.None, false)]
	public void Check_LogLevelValues_AreMappedCorrectly(string envVarValue, LogLevel logLevel, bool globalLogEnabled)
	{
		Check(OTEL_LOG_LEVEL, envVarValue, logLevel, globalLogEnabled);
		return;

		static void Check(string key, string envVarValue, LogLevel level, bool enabled)
		{
			var config = CreateConfig(key, envVarValue);
			config.LogLevel.Should().Be(level, "{0}", key);
			config.GlobalLogEnabled.Should().Be(enabled, "{0}", key);
		}
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("foo")]
	[InlineData("tracing")]
	public void Check_InvalidLogLevelValues_AreMappedToDefaultWarn(string? envVarValue)
	{
		Check(OTEL_LOG_LEVEL, envVarValue);
		return;

		static void Check(string key, string? envVarValue)
		{
			var config = CreateConfig(key, envVarValue);
			config.LogLevel.Should().Be(LogLevel.Warning, "{0}", key);
		}
	}

	[Fact]
	public void Check_LogDir_IsEvaluatedCorrectly()
	{
		Check(OTEL_DOTNET_AUTO_LOG_DIRECTORY, "/foo/bar");
		return;

		static void Check(string key, string envVarValue)
		{
			var config = CreateConfig(key, envVarValue);
			config.LogDirectory.Should().StartWith("/foo/bar", "{0}", key);
		}
	}

	[Theory]
	[InlineData(null, LogTargets.None)]
	[InlineData("", LogTargets.None)]
	[InlineData("foo", LogTargets.None)]
	[InlineData("foo,bar", LogTargets.None)]
	[InlineData("foo;bar", LogTargets.None)]
	[InlineData("file;foo;bar", LogTargets.File)]
	[InlineData("file", LogTargets.File)]
	[InlineData("stdout", LogTargets.StdOut)]
	[InlineData("StdOut", LogTargets.StdOut)]
	[InlineData("file;stdout", LogTargets.File | LogTargets.StdOut)]
	[InlineData("FILE;StdOut", LogTargets.File | LogTargets.StdOut)]
	[InlineData("file;stdout;file", LogTargets.File | LogTargets.StdOut)]
	[InlineData("FILE;StdOut;stdout", LogTargets.File | LogTargets.StdOut)]
	internal void Check_LogTargets_AreEvaluatedCorrectly(string? envVarValue, LogTargets? targets)
	{
		Check(ELASTIC_OTEL_LOG_TARGETS, envVarValue, targets);
		return;

		static void Check(string key, string? envVarValue, LogTargets? targets)
		{
			var config = CreateConfig(key, envVarValue);
			config.LogTargets.Should().Be(targets, "{0}", key);
		}
	}

	[Theory]
	[InlineData(LogLevel.Debug, null, LogTargets.StdOut, true, true)]
	[InlineData(LogLevel.Information, null, LogTargets.None, true, false)]
	[InlineData(LogLevel.Debug, null, LogTargets.File, false, true)]
	[InlineData(LogLevel.Information, null, LogTargets.None, false, false)]
	//Ensure explicit loglevel config always takes precedence
	[InlineData(LogLevel.Debug, "file", LogTargets.File, true, true)]
	[InlineData(LogLevel.Information, "file", LogTargets.File, false, true)]
	internal void LogTargetDefaultsToStandardOutIfRunningInContainerWithLogLevelDebug(LogLevel level, string? logTargetsEnvValue, LogTargets targets, bool inContainer, bool globalLogging)
	{
		var env = new Hashtable { { OTEL_LOG_LEVEL, level.ToString() } };
		if (inContainer)
			env.Add(DOTNET_RUNNING_IN_CONTAINER, "1");
		if (!string.IsNullOrWhiteSpace(logTargetsEnvValue))
			env.Add(ELASTIC_OTEL_LOG_TARGETS, logTargetsEnvValue);

		var config = new ElasticOpenTelemetryOptions(env);
		config.GlobalLogEnabled.Should().Be(globalLogging);
		config.LogTargets.Should().Be(targets);
	}

	private static ElasticOpenTelemetryOptions CreateConfig(string key, string? envVarValue)
	{
		var environment = new Hashtable { { key, envVarValue } };
		return new ElasticOpenTelemetryOptions(environment);
	}
}
