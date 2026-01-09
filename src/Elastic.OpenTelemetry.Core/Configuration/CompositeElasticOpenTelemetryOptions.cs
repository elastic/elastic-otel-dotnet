// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Elastic.OpenTelemetry.Configuration.Instrumentations;
using Elastic.OpenTelemetry.Configuration.Parsers;
using Elastic.OpenTelemetry.Core;
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
internal sealed class CompositeElasticOpenTelemetryOptions
{
	// These are the options that users can set via IConfiguration
	private static readonly string[] ElasticOpenTelemetryConfigKeys =
	[
		nameof(LogDirectory),
		nameof(LogLevel),
		nameof(LogTargets),
		nameof(SkipOtlpExporter),
		nameof(SkipInstrumentationAssemblyScanning)
	];

	internal static CompositeElasticOpenTelemetryOptions DefaultOptions = new();

	internal Guid InstanceId { get; } = Guid.NewGuid();

	private readonly EventLevel _eventLevel = EventLevel.Warning;

	private readonly ConfigCell<string?> _logDirectory = new(nameof(LogDirectory), null);
	private readonly ConfigCell<LogTargets?> _logTargets = new(nameof(LogTargets), null);

	private readonly ConfigCell<LogLevel?> _logLevel = new(nameof(LogLevel), LogLevel.Warning);
	private readonly ConfigCell<bool?> _skipOtlpExporter = new(nameof(SkipOtlpExporter), false);
	private readonly ConfigCell<bool?> _skipInstrumentationAssemblyScanning = new(nameof(SkipInstrumentationAssemblyScanning), false);
	private readonly ConfigCell<bool?> _runningInContainer = new(nameof(_runningInContainer), false);

	private readonly ConfigCell<Signals?> _signals = new(nameof(Signals), Signals.All);
	private readonly ConfigCell<TraceInstrumentations> _tracing = new(nameof(Tracing), TraceInstrumentations.All);
	private readonly ConfigCell<MetricInstrumentations> _metrics = new(nameof(Metrics), MetricInstrumentations.All);
	private readonly ConfigCell<LogInstrumentations> _logging = new(nameof(Logging), LogInstrumentations.All);

	private readonly IDictionary _environmentVariables;
	private readonly IConfiguration? _configuration;

	/// <summary>
	/// Creates a new instance of <see cref="CompositeElasticOpenTelemetryOptions"/> with properties
	/// bound from environment variables.
	/// </summary>
	internal CompositeElasticOpenTelemetryOptions() : this((IDictionary?)null)
	{
		if (BootstrapLogger.IsEnabled)
			BootstrapLogger.LogWithStackTrace($"{nameof(CompositeElasticOpenTelemetryOptions)}: Instance '{InstanceId}' created via parameterless ctor.");
	}

	internal CompositeElasticOpenTelemetryOptions(IDictionary? environmentVariables)
	{
		if (BootstrapLogger.IsEnabled)
		{
			BootstrapLogger.LogWithStackTrace($"{nameof(CompositeElasticOpenTelemetryOptions)}: Instance '{InstanceId}' created via ctor `(IDictionary? environmentVariables)`.");

			if (environmentVariables is null)
			{
				BootstrapLogger.Log($"CompositeElasticOpenTelemetryOptions(IDictionary): Param `environmentVariables` was `null`.");
			}
		}

		LogDirectoryDefault = GetDefaultLogDirectory();
		_environmentVariables = environmentVariables ?? GetEnvironmentVariables();

		if (BootstrapLogger.IsEnabled)
			BootstrapLogger.Log($"CompositeElasticOpenTelemetryOptions(IDictionary): Read {_environmentVariables.Count} environment variables");

		SetFromEnvironment(DOTNET_RUNNING_IN_CONTAINER, _runningInContainer, BoolParser);
		SetFromEnvironment(OTEL_DOTNET_AUTO_LOG_DIRECTORY, _logDirectory, StringParser);
		SetFromEnvironment(OTEL_LOG_LEVEL, _logLevel, LogLevelParser);
		SetFromEnvironment(ELASTIC_OTEL_LOG_TARGETS, _logTargets, LogTargetsParser);
		SetFromEnvironment(ELASTIC_OTEL_SKIP_OTLP_EXPORTER, _skipOtlpExporter, BoolParser);
		SetFromEnvironment(ELASTIC_OTEL_SKIP_ASSEMBLY_SCANNING, _skipInstrumentationAssemblyScanning, BoolParser);

		_eventLevel = ConfigurationParser.LogLevelToEventLevel(_logLevel.Value);

		if (BootstrapLogger.IsEnabled)
			BootstrapLogger.Log($"CompositeElasticOpenTelemetryOptions(IDictionary): Event log level set to: {_eventLevel}");

		var parser = new EnvironmentParser(_environmentVariables);
		parser.ParseInstrumentationVariables(_signals, _tracing, _metrics, _logging);
	}

	[UnconditionalSuppressMessage("AssemblyLoadTrimming", "IL2026:RequiresUnreferencedCode", Justification = "Manually verified")]
	[UnconditionalSuppressMessage("AssemblyLoadTrimming", "IL3050:RequiresDynamicCode", Justification = "Manually verified")]
	internal CompositeElasticOpenTelemetryOptions(IConfiguration? configuration, IDictionary? environmentVariables = null)
		: this(environmentVariables)
	{
		if (BootstrapLogger.IsEnabled)
			BootstrapLogger.LogWithStackTrace($"{nameof(CompositeElasticOpenTelemetryOptions)}: Instance '{InstanceId}' created via ctor `(IConfiguration? configuration, IDictionary? environmentVariables)`.");

		if (configuration is null)
		{
			BootstrapLogger.Log($"CompositeElasticOpenTelemetryOptions: Param `configuration` was `null`.");
			return;
		}

		var parser = new ConfigurationParser(configuration);

		parser.ParseLogDirectory(_logDirectory);
		parser.ParseLogTargets(_logTargets);
		parser.ParseLogLevel(_logLevel, ref _eventLevel);
		parser.ParseSkipOtlpExporter(_skipOtlpExporter);
		parser.ParseSkipInstrumentationAssemblyScanning(_skipInstrumentationAssemblyScanning);

		_configuration = configuration;
	}

	[UnconditionalSuppressMessage("AssemblyLoadTrimming", "IL2026:RequiresUnreferencedCode", Justification = "Manually verified")]
	[UnconditionalSuppressMessage("AssemblyLoadTrimming", "IL3050:RequiresDynamicCode", Justification = "Manually verified")]
	internal CompositeElasticOpenTelemetryOptions(IConfiguration configuration, ElasticOpenTelemetryOptions options)
		: this((IDictionary?)null)
	{
		if (BootstrapLogger.IsEnabled)
		{
			BootstrapLogger.LogWithStackTrace($"{nameof(CompositeElasticOpenTelemetryOptions)}: Instance '{InstanceId}'." +
				$"{NewLine}    Invoked with `{nameof(ElasticOpenTelemetryOptions)}` instance '{options.InstanceId}'.");
		}

		var parser = new ConfigurationParser(configuration);

		parser.ParseLogDirectory(_logDirectory);
		parser.ParseLogTargets(_logTargets);
		parser.ParseLogLevel(_logLevel, ref _eventLevel);
		parser.ParseSkipOtlpExporter(_skipOtlpExporter);
		parser.ParseSkipInstrumentationAssemblyScanning(_skipInstrumentationAssemblyScanning);

		if (BootstrapLogger.IsEnabled)
			BootstrapLogger.Log($"{nameof(CompositeElasticOpenTelemetryOptions)}: Configuration binding from IConfiguration completed.");

		if (options.SkipOtlpExporter.HasValue)
			_skipOtlpExporter.Assign(options.SkipOtlpExporter.Value, ConfigSource.Options);

		if (!string.IsNullOrEmpty(options.LogDirectory))
			_logDirectory.Assign(options.LogDirectory, ConfigSource.Options);

		if (options.LogLevel.HasValue)
			_logLevel.Assign(options.LogLevel.Value, ConfigSource.Options);

		if (options.LogTargets.HasValue)
			_logTargets.Assign(options.LogTargets.Value, ConfigSource.Options);

		if (options.SkipInstrumentationAssemblyScanning.HasValue)
			_skipInstrumentationAssemblyScanning.Assign(options.SkipInstrumentationAssemblyScanning.Value, ConfigSource.Options);

		AdditionalLogger = options.AdditionalLogger ?? options.AdditionalLoggerFactory?.CreateElasticLogger();

		if (BootstrapLogger.IsEnabled)
			BootstrapLogger.Log($"{nameof(CompositeElasticOpenTelemetryOptions)}: Configuration binding from user-provided `ElasticOpenTelemetryOptions` completed.");

		_configuration = configuration;
	}

	internal CompositeElasticOpenTelemetryOptions(ElasticOpenTelemetryOptions options)
		: this((IDictionary?)null)
	{
		if (BootstrapLogger.IsEnabled)
		{
			BootstrapLogger.LogWithStackTrace($"{nameof(CompositeElasticOpenTelemetryOptions)}: Instance '{InstanceId}' created via ctor `(ElasticOpenTelemetryOptions options)`." +
				$"{NewLine}    Invoked with `{nameof(ElasticOpenTelemetryOptions)}` instance '{options.InstanceId}' with hash '{RuntimeHelpers.GetHashCode(options)}'.");
		}

		// This should not happen, but just in case
		if (options is null)
			return;

		// Having configured the base settings from env vars, we now override anything that was
		// explicitly configured in the user provided options.

		if (options.SkipOtlpExporter.HasValue)
			_skipOtlpExporter.Assign(options.SkipOtlpExporter.Value, ConfigSource.Options);

		if (!string.IsNullOrEmpty(options.LogDirectory))
			_logDirectory.Assign(options.LogDirectory, ConfigSource.Options);

		if (options.LogLevel.HasValue)
			_logLevel.Assign(options.LogLevel.Value, ConfigSource.Options);

		if (options.LogTargets.HasValue)
			_logTargets.Assign(options.LogTargets.Value, ConfigSource.Options);

		if (options.SkipInstrumentationAssemblyScanning.HasValue)
			_skipInstrumentationAssemblyScanning.Assign(options.SkipInstrumentationAssemblyScanning.Value, ConfigSource.Options);

		AdditionalLogger = options.AdditionalLogger ?? options.AdditionalLoggerFactory?.CreateElasticLogger();

		if (BootstrapLogger.IsEnabled)
			BootstrapLogger.Log($"{nameof(CompositeElasticOpenTelemetryOptions)}: Configuration binding from user-provided `ElasticOpenTelemetryOptions` completed.");
	}

	/// <summary>
	/// Calculates whether global logging is enabled based on
	/// <see cref="LogTargets"/>, <see cref="LogDirectory"/> and <see cref="LogLevel"/>
	/// </summary>
	internal bool GlobalLogEnabled
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
		const string applicationMoniker = "elastic-otel-dotnet";

		string? directory;

		if (IsOSPlatform(OSPlatform.Windows))
			directory = Path.Combine(GetFolderPath(SpecialFolder.ApplicationData), "elastic", applicationMoniker);
		else if (IsOSPlatform(OSPlatform.OSX))
			directory = Path.Combine(GetFolderPath(SpecialFolder.LocalApplicationData), "elastic", applicationMoniker);
		else
			directory = $"/var/log/elastic/{applicationMoniker}";

		BootstrapLogger.Log($"Default log directory resolved to: {directory}");

		return directory;
	}

	/// <summary>
	/// The default log directory if file logging was enabled but non was specified
	/// <para>Defaults to: </para>
	/// <para> - %USERPROFILE%\AppData\Roaming\elastic\elastic-otel-dotnet (on Windows)</para>
	/// <para> - /var/log/elastic/elastic-otel-dotnet (on Linux)</para>
	/// <para> - ~/Library/Application Support/elastic/elastic-otel-dotnet (on OSX)</para>
	/// </summary>
	internal string LogDirectoryDefault { get; }

	/// <inheritdoc cref="ElasticOpenTelemetryOptions.LogDirectory"/>
	public string LogDirectory
	{
		get => _logDirectory.Value ?? LogDirectoryDefault;
		init => _logDirectory.Assign(value, ConfigSource.Property);
	}

	/// <summary>
	/// Used by <see cref="LoggingEventListener"/> to determine the appropiate event level to subscribe to
	/// </summary>
	internal EventLevel EventLogLevel => _eventLevel;

	/// <inheritdoc cref="ElasticOpenTelemetryOptions.LogLevel"/>
	public LogLevel LogLevel
	{
		get => _logLevel.Value ?? LogLevel.Warning;
		init => _logLevel.Assign(value, ConfigSource.Property);
	}

	/// <inheritdoc cref="ElasticOpenTelemetryOptions.LogTargets"/>
	public LogTargets LogTargets
	{
		get => _logTargets.Value ?? (GlobalLogEnabled
			? _runningInContainer.Value.HasValue && _runningInContainer.Value.Value ? LogTargets.StdOut : LogTargets.File
			: LogTargets.None);
		init => _logTargets.Assign(value, ConfigSource.Property);
	}

	/// <inheritdoc cref="ElasticOpenTelemetryOptions.SkipOtlpExporter"/>
	public bool SkipOtlpExporter
	{
		get => _skipOtlpExporter.Value ?? false;
		init => _skipOtlpExporter.Assign(value, ConfigSource.Property);
	}

	/// <inheritdoc cref="ElasticOpenTelemetryOptions.SkipInstrumentationAssemblyScanning"/>
	public bool SkipInstrumentationAssemblyScanning
	{
		get => _skipInstrumentationAssemblyScanning.Value ?? false;
		init => _skipInstrumentationAssemblyScanning.Assign(value, ConfigSource.Property);
	}

	public ILogger? AdditionalLogger { get; internal set; }

	/// <summary>
	/// Control which signals will be automatically enabled by the Elastic Distribution of OpenTelemetry .NET.
	/// <para>
	/// This configuration respects the open telemetry environment configuration out of the box:
	///	<list type="bullet">
	/// <item><see cref="OTEL_DOTNET_AUTO_TRACES_INSTRUMENTATION_ENABLED"/></item>
	/// <item><see cref="OTEL_DOTNET_AUTO_LOGS_INSTRUMENTATION_ENABLED"/></item>
	/// <item><see cref="OTEL_DOTNET_AUTO_METRICS_INSTRUMENTATION_ENABLED"/></item>
	/// </list>
	/// </para>
	/// <para>Setting this propery in code or configuration will take precedence over environment variables.</para>
	/// </summary>
	public Signals Signals
	{
		get => _signals.Value ?? Signals.All;
		init => _signals.Assign(value, ConfigSource.Property);
	}

	/// <summary>
	/// Enabled trace instrumentations.
	/// </summary>
	public TraceInstrumentations Tracing
	{
		get => _tracing.Value ?? TraceInstrumentations.All;
		init => _tracing.Assign(value, ConfigSource.Property);
	}

	/// <summary>
	/// Enabled trace instrumentations.
	/// </summary>
	public MetricInstrumentations Metrics
	{
		get => _metrics.Value ?? MetricInstrumentations.All;
		init => _metrics.Assign(value, ConfigSource.Property);
	}

	/// <summary>
	/// Enabled trace instrumentations.
	/// </summary>
	public LogInstrumentations Logging
	{
		get => _logging.Value ?? LogInstrumentations.All;
		init => _logging.Assign(value, ConfigSource.Property);
	}

	public override bool Equals(object? obj)
	{
		if (obj is not CompositeElasticOpenTelemetryOptions other)
			return false;

		return LogDirectory == other.LogDirectory &&
			   LogLevel == other.LogLevel &&
			   LogTargets == other.LogTargets &&
			   SkipOtlpExporter == other.SkipOtlpExporter &&
			   SkipInstrumentationAssemblyScanning == other.SkipInstrumentationAssemblyScanning &&
			   Signals == other.Signals &&
			   Tracing.SetEquals(other.Tracing) &&
			   Metrics.SetEquals(other.Metrics) &&
			   Logging.SetEquals(other.Logging) &&
			   ReferenceEquals(AdditionalLogger, other.AdditionalLogger);
	}

	public override int GetHashCode()
	{
#if NET462 || NETSTANDARD2_0
		return LogDirectory.GetHashCode()
			^ LogLevel.GetHashCode()
			^ LogTargets.GetHashCode()
			^ SkipOtlpExporter.GetHashCode()
			^ SkipInstrumentationAssemblyScanning.GetHashCode()
			^ Signals.GetHashCode()
			^ Tracing.GetHashCode()
			^ Metrics.GetHashCode()
			^ Logging.GetHashCode()
			^ (AdditionalLogger?.GetHashCode() ?? 0);
#else
		var hash1 = HashCode.Combine(LogDirectory, LogLevel, LogTargets, SkipOtlpExporter);
		var hash2 = HashCode.Combine(Signals, Tracing, Metrics, Logging, AdditionalLogger);
		var hash3 = HashCode.Combine(SkipInstrumentationAssemblyScanning);
		return HashCode.Combine(hash1, hash2, hash3);
#endif
	}

	private void SetFromEnvironment<T>(string key, ConfigCell<T> field, Func<string?, T?> parser)
	{
		var safeValue = GetSafeEnvironmentVariable(key);

		var value = parser(safeValue);

		if (value is null)
			return;

		field.Assign(value, ConfigSource.Environment);
	}

	private string GetSafeEnvironmentVariable(string key)
	{
		var value = _environmentVariables.Contains(key) ? _environmentVariables[key]?.ToString() : null;
		value ??= string.Empty;

		if (BootstrapLogger.IsEnabled)
			BootstrapLogger.Log($"{nameof(CompositeElasticOpenTelemetryOptions)}.{nameof(GetSafeEnvironmentVariable)}: Environment variable '{key}' = '{value}'");

		return value;
	}

	internal void LogConfigSources(ILogger logger)
	{
		LogConfig(logger, _logTargets);
		LogConfig(logger, _logDirectory);
		LogConfig(logger, _logLevel);

		LogConfig(logger, _signals);
		LogConfig(logger, _tracing);
		LogConfig(logger, _metrics);
		LogConfig(logger, _logging);

		LogConfig(logger, _skipOtlpExporter);
		LogConfig(logger, _skipInstrumentationAssemblyScanning);

		static void LogConfig<T>(ILogger logger, ConfigCell<T> cell)
		{
			// To reduce noise, we log as info only if not default, otherwise, log as debug
			if (cell.Source == ConfigSource.Default)
				logger.LogDebug("Configured value for {Configuration}", cell);
			else
				logger.LogInformation("Configured value for {Configuration}", cell);
		}
	}

	internal void LogApplicationConfigurationValues(ILogger logger)
	{
		if (_configuration is null)
			return;

		if (_configuration is IConfigurationRoot configRoot)
		{
			if (configRoot != null)
			{
				foreach (var provider in configRoot.Providers)
				{
					var providerName = provider.ToString();

					var keys = provider.GetChildKeys([], null);

					foreach (var key in keys)
					{
						if (!key.StartsWith("OTEL_", StringComparison.Ordinal))
							continue;

						if (provider.TryGet(key, out var value) && value is not null)
						{
							if (SensitiveEnvironmentVariables.Contains(key))
							{
								value = "<redacted>";
							}

							logger.LogInformation("IConfiguration [{providerName}] '{Key}' = '{Value}'", providerName, key, value);
						}
					}

					foreach (var key in ElasticOpenTelemetryConfigKeys)
					{
						if (provider.TryGet($"Elastic:OpenTelemetry:{key}", out var value) && value is not null)
						{
							logger.LogInformation("IConfiguration [{providerName}] '{Key}' = '{Value}'", providerName, key, value);
						}
					}
				}
			}
		}
	}
}
