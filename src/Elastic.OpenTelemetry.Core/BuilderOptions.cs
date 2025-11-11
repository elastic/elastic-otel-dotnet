namespace Elastic.OpenTelemetry.Core;

internal readonly record struct BuilderOptions<T>(
	Action<T>? UserProvidedConfigureBuilder,
	bool DeferAddOtlpExporter) where T : class;
