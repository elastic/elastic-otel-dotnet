// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections;
using System.Runtime.InteropServices;
using Elastic.OpenTelemetry.Configuration.Parsers;
using Elastic.OpenTelemetry.Diagnostics.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using static System.Environment;
using static System.Runtime.InteropServices.RuntimeInformation;
using static Elastic.OpenTelemetry.Configuration.EnvironmentVariables;
using static Elastic.OpenTelemetry.Configuration.Parsers.ConfigurationParsers;

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
	private static readonly string ConfigurationSection = "Elastic:OpenTelemetry";
	private static readonly string LogDirectoryConfigPropertyName = nameof(LogDirectory);
	private static readonly string LogLevelConfigPropertyName = nameof(LogLevel);
	private static readonly string LogTargetsConfigPropertyName = nameof(LogTargets);
	private static readonly string SkipOtlpExporterConfigPropertyName = nameof(SkipOtlpExporter);
	private static readonly string EnabledDefaultsConfigPropertyName = nameof(EnabledDefaults);
	private static readonly string EnabledSignalsConfigPropertyName = nameof(EnabledSignals);
	private static readonly string TraceInstrumentationsConfigPropertyName = nameof(TraceInstrumentations);

	private string? _logDirectory;
	private ConfigSource _logDirectorySource = ConfigSource.Default;

	private LogLevel? _logLevel;
	private ConfigSource _logLevelSource = ConfigSource.Default;

	private LogTargets? _logTargets;
	private ConfigSource _logTargetsSource = ConfigSource.Default;

	private readonly bool? _skipOtlpExporter;
	private readonly ConfigSource _skipOtlpExporterSource = ConfigSource.Default;

	private readonly ElasticDefaults? _elasticDefaults;
	private readonly ConfigSource _elasticDefaultsSource = ConfigSource.Default;

	private readonly bool? _runningInContainer;
	private readonly ConfigSource _runningInContainerSource = ConfigSource.Default;

	private readonly Signals? _signals;
	private readonly ConfigSource _signalsSource = ConfigSource.Default;

	private readonly HashSet<TraceInstrumentation> _traceInstrumentation;
	private readonly ConfigSource _traceInstrumentationSource = ConfigSource.Default;

	private string? _loggingSectionLogLevel;
	private readonly string _defaultLogDirectory;
	private readonly IDictionary _environmentVariables;

	/// <summary>
	/// Creates a new instance of <see cref="ElasticOpenTelemetryOptions"/> with properties
	/// bound from environment variables.
	/// </summary>
	public ElasticOpenTelemetryOptions(IDictionary? environmentVariables = null)
	{
		//TODO temp
		_traceInstrumentation = new HashSet<TraceInstrumentation>();

		_defaultLogDirectory = GetDefaultLogDirectory();
		_environmentVariables = environmentVariables ?? GetEnvironmentVariables();
		SetFromEnvironment(DOTNET_RUNNING_IN_CONTAINER, ref _runningInContainer, ref _runningInContainerSource, BoolParser);

		SetFromEnvironment(OTEL_DOTNET_AUTO_LOG_DIRECTORY, ref _logDirectory, ref _logDirectorySource, StringParser);
		SetFromEnvironment(OTEL_LOG_LEVEL, ref _logLevel, ref _logLevelSource, LogLevelParser);
		SetFromEnvironment(ELASTIC_OTEL_LOG_TARGETS, ref _logTargets, ref _logTargetsSource, LogTargetsParser);
		SetFromEnvironment(ELASTIC_OTEL_SKIP_OTLP_EXPORTER, ref _skipOtlpExporter, ref _skipOtlpExporterSource, BoolParser);
		SetFromEnvironment(ELASTIC_OTEL_ENABLE_ELASTIC_DEFAULTS, ref _elasticDefaults, ref _elasticDefaultsSource, EnabledDefaultsParser);

		var instrumentationParser = new InstrumentationParsers(_environmentVariables);
		instrumentationParser.Assign(ref _signals, ref _signalsSource);

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

		SetFromConfiguration(configuration, LogDirectoryConfigPropertyName, ref _logDirectory, ref _logDirectorySource, StringParser);
		SetFromConfiguration(configuration, LogLevelConfigPropertyName, ref _logLevel, ref _logLevelSource, LogLevelParser);
		SetFromConfiguration(configuration, LogTargetsConfigPropertyName, ref _logTargets, ref _logTargetsSource, LogTargetsParser);
		SetFromConfiguration(configuration, SkipOtlpExporterConfigPropertyName, ref _skipOtlpExporter, ref _skipOtlpExporterSource, BoolParser);
		SetFromConfiguration(configuration, EnabledDefaultsConfigPropertyName, ref _elasticDefaults, ref _elasticDefaultsSource, EnabledDefaultsParser);
		SetFromConfiguration(configuration, EnabledSignalsConfigPropertyName, ref _signals, ref _signalsSource, EnabledSignalsParser);

		BindFromLoggingSection(configuration);

		void BindFromLoggingSection(IConfiguration config)
		{
			// This will be used as a fallback if a more specific configuration is not provided.
			// We also store the logging level to use it within the logging event listener to determine the most verbose level to subscribe to.
			_loggingSectionLogLevel = config.GetValue<string>($"Logging:LogLevel:{CompositeLogger.LogCategory}");

			// Fall	back to the default logging level if the specific category is not configured.
			if (string.IsNullOrEmpty(_loggingSectionLogLevel))
				_loggingSectionLogLevel = config.GetValue<string>("Logging:LogLevel:Default");

			if (!string.IsNullOrEmpty(_loggingSectionLogLevel) && _logLevel is null)
			{
				_logLevel = LogLevelHelpers.ToLogLevel(_loggingSectionLogLevel);
				_logLevelSource = ConfigSource.IConfiguration;
			}
		}
	}

	/// <summary>
	/// Calculates whether global logging is enabled based on
	/// <see cref="LogTargets"/>, <see cref="LogDirectory"/> and <see cref="LogLevel"/>
	/// </summary>
	public bool GlobalLogEnabled
	{
		get
		{
			var isActive = _logLevel is <= LogLevel.Debug || !string.IsNullOrWhiteSpace(_logDirectory) || _logTargets.HasValue;
			if (!isActive)
				return isActive;

			if (_logLevel is LogLevel.None)
				isActive = false;
			else if (_logTargets is LogTargets.None)
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
	public string LogDirectoryDefault => _defaultLogDirectory;

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
		get => _logDirectory ?? LogDirectoryDefault;
		init
		{
			_logDirectory = value;
			_logDirectorySource = ConfigSource.Property;
		}
	}

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
		get => _logLevel ?? LogLevel.Warning;
		init
		{
			_logLevel = value;
			_logLevelSource = ConfigSource.Property;
		}
	}

	/// <inheritdoc cref="LogTargets"/>
	public LogTargets LogTargets
	{
		get => _logTargets ?? (GlobalLogEnabled
			? _runningInContainer.HasValue && _runningInContainer.Value ? LogTargets.StdOut : LogTargets.File
			: LogTargets.None);
		init
		{
			_logTargets = value;
			_logTargetsSource = ConfigSource.Property;
		}
	}

	/// <summary>
	/// Stops <see cref="ElasticOpenTelemetryBuilder"/> from registering OLTP exporters, useful for testing scenarios.
	/// </summary>
	public bool SkipOtlpExporter
	{
		get => _skipOtlpExporter ?? false;
		init
		{
			_skipOtlpExporter = value;
			_skipOtlpExporterSource = ConfigSource.Property;
		}
	}

	/// <summary>
	/// Allows flags to be set based of <see cref="ElasticDefaults"/> to selectively opt in to Elastic Distribution for OpenTelemetry .NET features.
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
	public ElasticDefaults EnabledDefaults
	{
		get => _elasticDefaults ?? ElasticDefaults.All;
		init
		{
			_elasticDefaults = value;
			_elasticDefaultsSource = ConfigSource.Property;
		}
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
	public Signals EnabledSignals
	{
		get => _signals ?? Signals.All;
		init
		{
			_signals = value;
			_signalsSource = ConfigSource.Property;
		}
	}

	/// <summary> Enabled trace instrumentations </summary>
	public HashSet<TraceInstrumentation> TraceInstrumentations
	{
		get => _traceInstrumentation;
		init
		{
			_traceInstrumentation = value;
			_traceInstrumentationSource = ConfigSource.Property;
		}
	}

	internal string? LoggingSectionLogLevel => _loggingSectionLogLevel;


	private void SetFromEnvironment<T>(string key, ref T field, ref ConfigSource configSourceField, Func<string?, (bool, T)> parser)
	{
		var (success, value) = parser(GetSafeEnvironmentVariable(key));

		if (success)
		{
			field = value;
			configSourceField = ConfigSource.Environment;
		}
	}

	private static void SetFromConfiguration<T>(IConfiguration configuration, string key, ref T field, ref ConfigSource configSourceField,
		Func<string?, (bool, T)> parser
	)
	{
		var lookup = configuration.GetValue<string>($"{ConfigurationSection}:{key}");
		var (success, parsed) = parser(lookup);

		if (success)
		{
			field = parsed;
			configSourceField = ConfigSource.IConfiguration;
		}
	}

	private string GetSafeEnvironmentVariable(string key)
	{
		var value = _environmentVariables.Contains(key) ? _environmentVariables[key]?.ToString() : null;
		return value ?? string.Empty;
	}


	internal void LogConfigSources(ILogger logger)
	{
		logger.LogInformation("Configured value for {ConfigKey}: '{ConfigValue}' from [{ConfigSource}]",
			LogDirectoryConfigPropertyName, _logDirectory, _logDirectorySource);

		logger.LogInformation("Configured value for {ConfigKey}: '{ConfigValue}' from [{ConfigSource}]",
			LogLevelConfigPropertyName, _logLevel, _logLevelSource);

		logger.LogInformation("Configured value for {ConfigKey}: '{ConfigValue}' from [{ConfigSource}]",
			SkipOtlpExporterConfigPropertyName, _skipOtlpExporter, _skipOtlpExporterSource);

		logger.LogInformation("Configured value for {ConfigKey}: '{ConfigValue}' from [{ConfigSource}]",
			EnabledDefaultsConfigPropertyName, _elasticDefaults, _elasticDefaultsSource);

		logger.LogInformation("Configured value for {ConfigKey}: '{ConfigValue}' from [{ConfigSource}]",
			EnabledSignalsConfigPropertyName, _signals, _signalsSource);

		logger.LogInformation("Configured value for {ConfigKey}: '{ConfigValue}' from [{ConfigSource}]",
			TraceInstrumentationsConfigPropertyName, _traceInstrumentation, _traceInstrumentationSource);
	}


	internal enum ConfigSource
	{
		Default, // Default value assigned within this class
		Environment, // Loaded from an environment variable
		IConfiguration, // Bound from an IConfiguration instance
		Property // Set via property initializer
	}

}
