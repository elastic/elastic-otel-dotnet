// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Core;
using OpenTelemetry.Exporter;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Elastic.OpenTelemetry.Exporters;
#pragma warning restore IDE0130 // Namespace does not match folder structure

internal static class OtlpExporterDefaults
{
	private static string UserAgent => $"elastic-otlp-dotnet/{VersionHelper.InformationalVersion}";

	internal static void OtlpExporterOptions(OtlpExporterOptions options) =>
		options.UserAgentProductIdentifier = UserAgent;

	internal static OtlpExporterOptions ConfigureElasticUserAgent(this OtlpExporterOptions options)
	{
		OtlpExporterOptions(options);
		return options;
	}
}
