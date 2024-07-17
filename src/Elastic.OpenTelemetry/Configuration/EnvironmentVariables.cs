// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.OpenTelemetry.Configuration;

internal static class EnvironmentVariables
{
	// ReSharper disable InconsistentNaming
	// ReSharper disable IdentifierTypo
	public const string ELASTIC_OTEL_SKIP_OTLP_EXPORTER = nameof(ELASTIC_OTEL_SKIP_OTLP_EXPORTER);

	public const string OTEL_DOTNET_AUTO_LOG_DIRECTORY = nameof(OTEL_DOTNET_AUTO_LOG_DIRECTORY);
	public const string OTEL_LOG_LEVEL = nameof(OTEL_LOG_LEVEL);

	public const string ELASTIC_OTEL_LOG_TARGETS = nameof(ELASTIC_OTEL_LOG_TARGETS);

	public const string DOTNET_RUNNING_IN_CONTAINER = nameof(DOTNET_RUNNING_IN_CONTAINER);

	public const string ELASTIC_OTEL_ENABLE_ELASTIC_DEFAULTS = nameof(ELASTIC_OTEL_ENABLE_ELASTIC_DEFAULTS);


	public const string OTEL_DOTNET_AUTO_INSTRUMENTATION_ENABLED = nameof(OTEL_DOTNET_AUTO_INSTRUMENTATION_ENABLED);

	public const string OTEL_DOTNET_AUTO_TRACES_INSTRUMENTATION_ENABLED = nameof(OTEL_DOTNET_AUTO_TRACES_INSTRUMENTATION_ENABLED);
	public const string OTEL_DOTNET_AUTO_METRICS_INSTRUMENTATION_ENABLED = nameof(OTEL_DOTNET_AUTO_METRICS_INSTRUMENTATION_ENABLED);
	public const string OTEL_DOTNET_AUTO_LOGS_INSTRUMENTATION_ENABLED = nameof(OTEL_DOTNET_AUTO_LOGS_INSTRUMENTATION_ENABLED);

	// ReSharper enable IdentifierTypo
	// ReSharper enable InconsistentNaming
}
