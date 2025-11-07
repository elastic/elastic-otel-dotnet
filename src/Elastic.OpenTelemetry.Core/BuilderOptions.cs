namespace Elastic.OpenTelemetry.Core;

internal readonly record struct BuilderOptions<T>(
	Action<T>? UserProvidedConfigureBuilder,
	bool DeferAddOtlpExporter,
	bool SkipLogCallerInfo,
	string? CalleeName = null) where T : class;
