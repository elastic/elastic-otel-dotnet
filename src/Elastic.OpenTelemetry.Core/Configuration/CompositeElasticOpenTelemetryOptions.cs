// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
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
internal sealed class CompositeElasticOpenTelemetryOptions : ICentralConfigurationSubscriber
{
	private const string ServiceNameResourceAttributeKey = "service.name";
	private const string ServiceVersionResourceAttributeKey = "service.name";
	private const string AuthorizationHeaderPrefix = "Authorization=ApiKey";

	// These are the options that users can set via IConfiguration
	private static readonly string[] ElasticOpenTelemetryConfigKeys =
	[
		nameof(LogDirectory),
		nameof(LogLevel),
		nameof(LogTargets),
		nameof(SkipOtlpExporter),
		nameof(SkipInstrumentationAssemblyScanning),
		nameof(OpAmpEndpoint),
		nameof(OpAmpHeaders)
	];

	internal static CompositeElasticOpenTelemetryOptions DefaultOptions = new();

	internal Guid InstanceId { get; } = Guid.NewGuid();

	private readonly ConfigCell<string?> _logDirectory = new(nameof(LogDirectory), null);
	private readonly ConfigCell<LogTargets?> _logTargets = new(nameof(LogTargets), null);
	private readonly ConfigCell<LogLevel?> _logLevel = new(nameof(LogLevel), LogLevel.Warning);
	private readonly ConfigCell<bool?> _skipOtlpExporter = new(nameof(SkipOtlpExporter), false);
	private readonly ConfigCell<bool?> _skipInstrumentationAssemblyScanning = new(nameof(SkipInstrumentationAssemblyScanning), false);
	private readonly ConfigCell<bool?> _runningInContainer = new(nameof(_runningInContainer), false);

	private readonly ConfigCell<string?> _opAmpEndpoint = new(nameof(OpAmpEndpoint), null);
	private readonly ConfigCell<string?> _opAmpHeaders = new(nameof(ServiceName), null);

	private readonly ConfigCell<Signals?> _signals = new(nameof(Signals), Signals.All);
	private readonly ConfigCell<TraceInstrumentations> _tracing = new(nameof(Tracing), TraceInstrumentations.All);
	private readonly ConfigCell<MetricInstrumentations> _metrics = new(nameof(Metrics), MetricInstrumentations.All);
	private readonly ConfigCell<LogInstrumentations> _logging = new(nameof(Logging), LogInstrumentations.All);

	// These are not settable via IConfiguration or ElasticOpenTelemetryOptions, only env vars
	private readonly ConfigCell<string?> _resourceAttributes = new(nameof(ResourceAttributes), null);

	private readonly IDictionary _environmentVariables;
	private readonly IConfiguration? _configuration;

	private bool? _isOpAmpEnabled;

	/// <summary>
	/// Creates a new instance of <see cref="CompositeElasticOpenTelemetryOptions"/> with properties
	/// bound from environment variables.
	/// </summary>
	internal CompositeElasticOpenTelemetryOptions() : this((IDictionary?)null)
	{
		if (BootstrapLogger.IsEnabled)
			BootstrapLogger.LogWithStackTrace($"{nameof(CompositeElasticOpenTelemetryOptions)}: Instance '{InstanceId}' created via parameterless ctor.");
	}

	/// <summary>
	/// Creates a new instance of <see cref="CompositeElasticOpenTelemetryOptions"/> with properties
	/// bound from environment variables.
	/// </summary>
	internal CompositeElasticOpenTelemetryOptions(IDictionary? environmentVariables)
	{
		if (BootstrapLogger.IsEnabled)
			BootstrapLogger.LogWithStackTrace($"{nameof(CompositeElasticOpenTelemetryOptions)}: Instance '{InstanceId}' created via ctor `(IDictionary? environmentVariables)`.");

		if (environmentVariables is null)
		{
			BootstrapLogger.Log($"CompositeElasticOpenTelemetryOptions: Param `environmentVariables` was `null`.");
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
		SetFromEnvironment(ELASTIC_OTEL_OPAMP_ENDPOINT, _opAmpEndpoint, StringParser);
		SetFromEnvironment(ELASTIC_OTEL_OPAMP_HEADERS, _opAmpHeaders, StringParser);
		SetFromEnvironment(OTEL_RESOURCE_ATTRIBUTES, _resourceAttributes, StringParser);

		var parser = new EnvironmentParser(_environmentVariables);
		parser.ParseInstrumentationVariables(_signals, _tracing, _metrics, _logging);

		if (BootstrapLogger.IsEnabled)
			BootstrapLogger.Log($"{nameof(CompositeElasticOpenTelemetryOptions)}: Configuration binding from environment variables completed.");
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

		AssignFromIConfiguration(configuration);

		// We store this so we can log any application configuration values later, if needed.
		_configuration = configuration;
	}

	private void AssignFromIConfiguration(IConfiguration configuration)
	{
		var parser = new ConfigurationParser(configuration);

		parser.ParseLogDirectory(_logDirectory);
		parser.ParseLogTargets(_logTargets);
		parser.ParseLogLevel(_logLevel);
		parser.ParseSkipOtlpExporter(_skipOtlpExporter);
		parser.ParseSkipInstrumentationAssemblyScanning(_skipInstrumentationAssemblyScanning);
		parser.ParseOpAmpEndpoint(_opAmpEndpoint);
		parser.ParseResourceAttributes(_resourceAttributes);

		if (BootstrapLogger.IsEnabled)
			BootstrapLogger.Log($"{nameof(CompositeElasticOpenTelemetryOptions)}: Configuration binding from IConfiguration completed.");
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

		AssignFromIConfiguration(configuration);
		AssignFromElasticOpenTelemetryOptions(options);

		// We store this so we can log any application configuration values later, if needed.
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

		// Having configured the base settings from env vars, we now override anything that was
		// explicitly configured in the user provided options.

		AssignFromElasticOpenTelemetryOptions(options);
	}

	private void AssignFromElasticOpenTelemetryOptions(ElasticOpenTelemetryOptions options)
	{
		// This should not happen, but just in case
		if (options is null)
			return;

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

		if (options.OpAmpEndpoint is not null)
			_opAmpEndpoint.Assign(options.OpAmpEndpoint, ConfigSource.Options);

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

			// Determine if logging is initially active based on:
			// - Log level being Debug or more verbose (lower value)
			// - Log directory being explicitly configured
			// - Log targets having a value
			var isActive = level is <= LogLevel.Debug || !string.IsNullOrWhiteSpace(_logDirectory.Value) || targets.HasValue;

			// If none of the above conditions are met, return false immediately
			if (!isActive)
				return isActive;

			// If log level is explicitly set to None, disable logging
			if (level is LogLevel.None)
				isActive = false;

			// If log targets is explicitly set to None, disable logging
			else if (targets is LogTargets.None)
				isActive = false;

			// Return the final determination of whether global logging should be enabled
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
	internal string LogDirectory
	{
		get => _logDirectory.Value ?? LogDirectoryDefault;
		init => _logDirectory.Assign(value, ConfigSource.Property);
	}

	/// <inheritdoc cref="ElasticOpenTelemetryOptions.LogLevel"/>
	internal LogLevel LogLevel
	{
		get => _logLevel.Value ?? LogLevel.Warning;
		init => _logLevel.Assign(value, ConfigSource.Property);
	}

	/// <inheritdoc cref="ElasticOpenTelemetryOptions.LogTargets"/>
	internal LogTargets LogTargets
	{
		get => _logTargets.Value ?? (GlobalLogEnabled
			? _runningInContainer.Value.HasValue && _runningInContainer.Value.Value ? LogTargets.StdOut : LogTargets.File
			: LogTargets.None);
		init => _logTargets.Assign(value, ConfigSource.Property);
	}

	/// <inheritdoc cref="ElasticOpenTelemetryOptions.SkipOtlpExporter"/>
	internal bool SkipOtlpExporter
	{
		get => _skipOtlpExporter.Value ?? false;
		init => _skipOtlpExporter.Assign(value, ConfigSource.Property);
	}

	/// <inheritdoc cref="ElasticOpenTelemetryOptions.SkipInstrumentationAssemblyScanning"/>
	internal bool SkipInstrumentationAssemblyScanning
	{
		get => _skipInstrumentationAssemblyScanning.Value ?? false;
		init => _skipInstrumentationAssemblyScanning.Assign(value, ConfigSource.Property);
	}

	/// <inheritdoc cref="ElasticOpenTelemetryOptions.OpAmpEndpoint"/>
	internal string? OpAmpEndpoint
	{
		get => _opAmpEndpoint.Value ?? null;
		init => _opAmpEndpoint.Assign(value, ConfigSource.Property);
	}

	/// <inheritdoc cref="ElasticOpenTelemetryOptions.OpAmpHeaders"/>
	internal string? OpAmpHeaders
	{
		get => _opAmpHeaders.Value ?? null;
		init => _opAmpHeaders.Assign(value, ConfigSource.Property);
	}

	internal string? ServiceName { get; private set; }

	internal string? ServiceVersion { get; private set; }

	internal string? ResourceAttributes
	{
		get => _opAmpEndpoint.Value ?? null;
		init => _opAmpEndpoint.Assign(value, ConfigSource.Property);
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
			   OpAmpEndpoint == other.OpAmpEndpoint &&
			   OpAmpHeaders == other.OpAmpHeaders &&
			   ResourceAttributes == other.ResourceAttributes &&
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
			 ^ (OpAmpEndpoint?.GetHashCode() ?? 0)
			 ^ (OpAmpHeaders?.GetHashCode() ?? 0)
			 ^ (ResourceAttributes?.GetHashCode() ?? 0)
			 ^ (AdditionalLogger?.GetHashCode() ?? 0);
#else
		var hash1 = HashCode.Combine(LogDirectory, LogLevel, LogTargets, SkipOtlpExporter);
		var hash2 = HashCode.Combine(Signals, Tracing, Metrics, Logging, AdditionalLogger);
		var hash3 = HashCode.Combine(SkipInstrumentationAssemblyScanning, OpAmpEndpoint, OpAmpHeaders, ResourceAttributes);
		return HashCode.Combine(hash1, hash2, hash3);
#endif
	}

	internal bool IsOpAmpEnabled()
	{
		// If we've already evaluated the configuration, return the cached value.
		if (_isOpAmpEnabled.HasValue)
			return _isOpAmpEnabled.Value;

		// OpAMP requires an endpoint, an auth header and resource attributes to function.
		if (string.IsNullOrEmpty(OpAmpEndpoint) || string.IsNullOrEmpty(OpAmpHeaders) || string.IsNullOrEmpty(ResourceAttributes))
		{
			return SetAndReturn(false);
		}

		if (!OpAmpHeaders!.Contains(AuthorizationHeaderPrefix, StringComparison.OrdinalIgnoreCase))
		{
			return SetAndReturn(false);
		}

		// OpAMP requires at minimum the service.name to be set so central configuration can locate the correct configuration.
		ServiceName = ExtractValueForKey(ResourceAttributes!, ServiceNameResourceAttributeKey);

		if (string.IsNullOrEmpty(ServiceName))
		{
			return SetAndReturn(false);
		}

		// Optionally extract and cache service version if present
		ServiceVersion = ExtractValueForKey(ResourceAttributes!, ServiceVersionResourceAttributeKey);

		return SetAndReturn(true);

		bool SetAndReturn(bool value)
		{
			_isOpAmpEnabled = value;
			return value;
		}
	}

	private static string? ExtractValueForKey(string input, string key)
	{
		if (string.IsNullOrEmpty(input))
			return null;

		var span = input.AsSpan();

		var index = span.IndexOf(key, StringComparison.Ordinal);

		if (index == -1)
			return null;

		span = span[(index + key.Length)..];

		if (span.Length == 0 || span[0] != '=')
			return null;

		index = span.IndexOf(',');

		return index == -1 ? span[1..].ToString() : span.Slice(1, index).ToString();
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

		LogConfig(logger, _opAmpEndpoint);
		// TODO - Log headers but redact auth header value
		LogConfig(logger, _resourceAttributes);

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

	public void OnConfiguration(RemoteConfiguration remoteConfiguration) =>
		_logLevel.Assign(remoteConfiguration.LogLevel, ConfigSource.CentralConfiguration);
}
