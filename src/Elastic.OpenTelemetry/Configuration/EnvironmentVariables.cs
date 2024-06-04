// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.OpenTelemetry.Configuration;

internal static class EnvironmentVariables
{
	// ReSharper disable InconsistentNaming
	public const string ELASTIC_OTEL_SKIP_OTLP_EXPORTER = nameof(ELASTIC_OTEL_SKIP_OTLP_EXPORTER);

	public const string ELASTIC_OTEL_LOG_DIRECTORY = nameof(ELASTIC_OTEL_LOG_DIRECTORY);
	public const string ELASTIC_OTEL_LOG_LEVEL = nameof(ELASTIC_OTEL_LOG_LEVEL);
	public const string ELASTIC_OTEL_LOG_TARGETS = nameof(ELASTIC_OTEL_LOG_TARGETS);

	public const string ELASTIC_OTEL_ENABLE_ELASTIC_DEFAULTS = nameof(ELASTIC_OTEL_ENABLE_ELASTIC_DEFAULTS);
	// ReSharper enable InconsistentNaming
}
