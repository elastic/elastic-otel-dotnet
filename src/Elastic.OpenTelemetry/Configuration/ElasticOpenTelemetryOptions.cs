// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections;
using System.Diagnostics.Tracing;
using System.Runtime.InteropServices;
using Elastic.OpenTelemetry.Configuration.Instrumentations;
using Elastic.OpenTelemetry.Configuration.Parsers;
using Elastic.OpenTelemetry.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using static System.Environment;
using static System.Runtime.InteropServices.RuntimeInformation;
using static Elastic.OpenTelemetry.Configuration.EnvironmentVariables;
using static Elastic.OpenTelemetry.Configuration.Parsers.SharedParsers;

namespace Elastic.OpenTelemetry.Configuration;

/// <summary>
/// Defines advanced options which can be used to finely-tune the behaviour of the Elastic
/// distribution of OpenTelemetry.
/// </summary>
/// <remarks>
/// Options are bound from the following sources:
/// <list type="bullet">
/// <item><description>Environment variables</description></item>
/// <item><description>An <see cref="IConfiguration"/> instance</description></item>
/// </list>
/// Options initialised via property initializers take precedence over bound values.
/// Environment variables take precedence over <see cref="IConfiguration"/> values.
/// </remarks>
public class ElasticOpenTelemetryOptions
{
	private readonly ConfigCell<string?> _logDirectory = new(nameof(LogDirectory), null);
	private readonly ConfigCell<LogTargets?> _logTargets = new(nameof(LogTargets), null);

	private readonly EventLevel _eventLevel = EventLevel.Informational;
	private readonly ConfigCell<LogLevel?> _logLevel = new(nameof(LogLevel), LogLevel.Warning);
	private readonly ConfigCell<bool?> _skipOtlpExporter = new(nameof(SkipOtlpExporter), false);
	private readonly ConfigCell<ElasticDefaults?> _enabledDefaults = new(nameof(ElasticDefaults), ElasticDefaults.All);
	private readonly ConfigCell<bool?> _runningInContainer = new(nameof(_runningInContainer), false);
	private readonly ConfigCell<Signals?> _signals = new(nameof(Signals), Signals.All);

	private readonly ConfigCell<TraceInstrumentations> _tracing = new(nameof(Tracing), TraceInstrumentations.All);
	private readonly ConfigCell<MetricInstrumentations> _metrics = new(nameof(Metrics), MetricInstrumentations.All);
	private readonly ConfigCell<LogInstrumentations> _logging = new(nameof(Logging), LogInstrumentations.All);

	private readonly IDictionary _environmentVariables;

	/// <summary>
	/// Creates a new instance of <see cref="ElasticOpenTelemetryOptions"/> with properties
	/// bound from environment variables.
	/// </summary>
	public ElasticOpenTelemetryOptions(IDictionary? environmentVariables = null)
	{
		LogDirectoryDefault = GetDefaultLogDirectory();
		_environmentVariables = environmentVariables ?? GetEnvironmentVariables();
		SetFromEnvironment(DOTNET_RUNNING_IN_CONTAINER, _runningInContainer, BoolParser);

		SetFromEnvironment(OTEL_DOTNET_AUTO_LOG_DIRECTORY, _logDirectory, StringParser);
		SetFromEnvironment(OTEL_LOG_LEVEL, _logLevel, LogLevelParser);
		SetFromEnvironment(ELASTIC_OTEL_LOG_TARGETS, _logTargets, LogTargetsParser);
		SetFromEnvironment(ELASTIC_OTEL_SKIP_OTLP_EXPORTER, _skipOtlpExporter, BoolParser);
		SetFromEnvironment(ELASTIC_OTEL_DEFAULTS_ENABLED, _enabledDefaults, ElasticDefaultsParser);

		var parser = new EnvironmentParser(_environmentVariables);
		parser.ParseInstrumentationVariables(_signals, _tracing, _metrics, _logging);

	}

	/// <summary>
	/// Creates a new instance of <see cref="ElasticOpenTelemetryOptions"/> with properties
	/// bound from environment variables and an <see cref="IConfiguration"/> instance.
	/// </summary>
	internal ElasticOpenTelemetryOptions(IConfiguration? configuration, IDictionary? environmentVariables = null)
		: this(environmentVariables)
	{
		if (configuration is null)
			return;

		var parser = new ConfigurationParser(configuration);
		parser.ParseLogDirectory(_logDirectory);
		parser.ParseLogTargets(_logTargets);
		parser.ParseLogLevel(_logLevel, ref _eventLevel);
		parser.ParseSkipOtlpExporter(_skipOtlpExporter);
		parser.ParseElasticDefaults(_enabledDefaults);
		parser.ParseSignals(_signals);

		parser.ParseInstrumentations(_tracing, _metrics, _logging);

	}

	/// <summary>
	/// Calculates whether global logging is enabled based on
	/// <see cref="LogTargets"/>, <see cref="LogDirectory"/> and <see cref="LogLevel"/>
	/// </summary>
	public bool GlobalLogEnabled
	{
		get
		{
			var level = _logLevel.Value;
			var targets = _logTargets.Value;
			var isActive = level is <= LogLevel.Debug || !string.IsNullOrWhiteSpace(_logDirectory.Value) || targets.HasValue;
			if (!isActive)
				return isActive;

			if (level is LogLevel.None)
				isActive = false;
			else if (targets is LogTargets.None)
				isActive = false;
			return isActive;
		}
	}

	private static string GetDefaultLogDirectory()
	{
		var applicationMoniker = "elastic-otel-dotnet";
		if (IsOSPlatform(OSPlatform.Windows))
			return Path.Combine(GetFolderPath(SpecialFolder.ApplicationData), "elastic", applicationMoniker);
		if (IsOSPlatform(OSPlatform.OSX))
			return Path.Combine(GetFolderPath(SpecialFolder.LocalApplicationData), "elastic", applicationMoniker);

		return $"/var/log/elastic/{applicationMoniker}";
	}

	/// <summary>
	/// The default log directory if file logging was enabled but non was specified
	/// <para>Defaults to: </para>
	/// <para> - %PROGRAMDATA%\elastic\apm-agent-dotnet (on Windows)</para>
	/// <para> - /var/log/elastic/apm-agent-dotnet (on Linux)</para>
	/// <para> - ~/Library/Application_Support/elastic/apm-agent-dotnet (on OSX)</para>
	/// </summary>
	public string LogDirectoryDefault { get; }

	/// <summary>
	/// The output directory where the Elastic Distribution for OpenTelemetry .NET will write log files.
	/// </summary>
	/// <remarks>
	/// When configured, a file log will be created in this directory with the name
	/// <c>{ProcessName}_{UtcUnixTimeMilliseconds}_{ProcessId}.instrumentation.log</c>.
	/// This log file includes log messages from the OpenTelemetry SDK and the Elastic distribution.
	/// </remarks>
	public string LogDirectory
	{
		get => _logDirectory.Value ?? LogDirectoryDefault;
		init => _logDirectory.Assign(value, ConfigSource.Property);
	}

	/// <summary>
	/// Used by <see cref="LoggingEventListener"/> to determine the appropiate event level to subscribe to
	/// </summary>
	internal EventLevel EventLogLevel => _eventLevel;

	/// <summary>
	/// The log level to use when writing log files.
	/// </summary>
	/// <remarks>
	/// Valid values are:
	/// <list type="bullet">
	/// <item><term>None</term><description>Disables logging.</description></item>
	/// <item><term>Critical</term><description>Failures that require immediate attention.</description></item>
	/// <item><term>Error</term><description>Errors and exceptions that cannot be handled.</description></item>
	/// <item><term>Warning</term><description>Abnormal or unexpected events.</description></item>
	/// <item><term>Information</term><description>General information about the distribution and OpenTelemetry SDK.</description></item>
	/// <item><term>Debug</term><description>Rich debugging and development.</description></item>
	/// <item><term>Trace</term><description>Contain the most detailed messages.</description></item>
	/// </list>
	/// </remarks>
	public LogLevel LogLevel
	{
		get => _logLevel.Value ?? LogLevel.Warning;
		init => _logLevel.Assign(value, ConfigSource.Property);
	}

	/// <inheritdoc cref="LogTargets"/>
	public LogTargets LogTargets
	{
		get => _logTargets.Value ?? (GlobalLogEnabled
			? _runningInContainer.Value.HasValue && _runningInContainer.Value.Value ? LogTargets.StdOut : LogTargets.File
			: LogTargets.None);
		init => _logTargets.Assign(value, ConfigSource.Property);
	}

	/// <summary>
	/// Stops <see cref="ElasticOpenTelemetryBuilder"/> from registering OLTP exporters, useful for testing scenarios.
	/// </summary>
	public bool SkipOtlpExporter
	{
		get => _skipOtlpExporter.Value ?? false;
		init => _skipOtlpExporter.Assign(value, ConfigSource.Property);
	}

	/// <summary>
	/// Allows flags to be set based of <see cref="Configuration.ElasticDefaults"/> to selectively opt in to Elastic Distribution for OpenTelemetry .NET features.
	/// <para>Defaults to <see cref="ElasticDefaults.All"/></para>
	/// </summary>
	/// <remarks>
	/// Valid values are:
	/// <list type="bullet">
	/// <item><term>None</term><description> Disables all Elastic defaults resulting in the use of the "vanilla" SDK.</description></item>
	/// <item><term>All</term><description> Enables all defaults (default if this option is not specified).</description></item>
	/// <item><term>Tracing</term><description> Enables Elastic defaults for tracing.</description></item>
	/// <item><term>Metrics</term><description> Enables Elastic defaults for metrics.</description></item>
	/// <item><term>Logging</term><description> Enables Elastic defaults for logging.</description></item>
	/// </list>
	/// </remarks>
	public ElasticDefaults ElasticDefaults
	{
		get => _enabledDefaults.Value ?? ElasticDefaults.All;
		init => _enabledDefaults.Assign(value, ConfigSource.Property);
	}

	/// <summary>
	/// Control which signals will be automatically enabled by the Elastic Distribution for OpenTelemetry .NET.
	/// <para>
	/// This configuration respects the open telemetry environment configuration out of the box:
	///	<list type="bullet">
	/// <item><see cref="EnvironmentVariables.OTEL_DOTNET_AUTO_TRACES_INSTRUMENTATION_ENABLED"/></item>
	/// <item><see cref="EnvironmentVariables.OTEL_DOTNET_AUTO_LOGS_INSTRUMENTATION_ENABLED"/></item>
	/// <item><see cref="EnvironmentVariables.OTEL_DOTNET_AUTO_METRICS_INSTRUMENTATION_ENABLED"/></item>
	/// </list>
	/// </para>
	/// <para>Setting this propery in code or configuration will take precedence over environment variables</para>
	/// </summary>
	public Signals Signals
	{
		get => _signals.Value ?? Signals.All;
		init => _signals.Assign(value, ConfigSource.Property);
	}

	/// <summary> Enabled trace instrumentations </summary>
	public TraceInstrumentations Tracing
	{
		get => _tracing.Value ?? TraceInstrumentations.All;
		init => _tracing.Assign(value, ConfigSource.Property);
	}

	/// <summary> Enabled trace instrumentations </summary>
	public MetricInstrumentations Metrics
	{
		get => _metrics.Value ?? MetricInstrumentations.All;
		init => _metrics.Assign(value, ConfigSource.Property);
	}

	/// <summary> Enabled trace instrumentations </summary>
	public LogInstrumentations Logging
	{
		get => _logging.Value ?? LogInstrumentations.All;
		init => _logging.Assign(value, ConfigSource.Property);
	}

	private void SetFromEnvironment<T>(string key, ConfigCell<T> field, Func<string?, T?> parser)
	{
		var value = parser(GetSafeEnvironmentVariable(key));
		if (value is null)
			return;

		field.Assign(value, ConfigSource.Environment);

	}

	private string GetSafeEnvironmentVariable(string key)
	{
		var value = _environmentVariables.Contains(key) ? _environmentVariables[key]?.ToString() : null;
		return value ?? string.Empty;
	}


	internal void LogConfigSources(ILogger logger)
	{
		logger.LogInformation("Configured value for {Configuration}", _logDirectory);
		logger.LogInformation("Configured value for {Configuration}", _logLevel);
		logger.LogInformation("Configured value for {Configuration}", _skipOtlpExporter);
		logger.LogInformation("Configured value for {Configuration}", _enabledDefaults);
		logger.LogInformation("Configured value for {Configuration}", _signals);
		logger.LogInformation("Configured value for {Configuration}", _tracing);
		logger.LogInformation("Configured value for {Configuration}", _metrics);
		logger.LogInformation("Configured value for {Configuration}", _logging);
	}
}
