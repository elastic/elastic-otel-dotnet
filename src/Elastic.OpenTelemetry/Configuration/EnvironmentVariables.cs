// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.OpenTelemetry.Configuration;

internal static class EnvironmentVariables
{
	public const string ElasticOtelSkipOtlpExporter = "ELASTIC_OTEL_SKIP_OTLP_EXPORTER";
	public const string ElasticOtelFileLogDirectoryEnvironmentVariable = "ELASTIC_OTEL_FILE_LOG_DIRECTORY";
	public const string ElasticOtelFileLogLevelEnvironmentVariable = "ELASTIC_OTEL_FILE_LOG_LEVEL";
	public const string ElasticOtelEnableElasticDefaults = "ELASTIC_OTEL_ENABLE_ELASTIC_DEFAULTS";
}
