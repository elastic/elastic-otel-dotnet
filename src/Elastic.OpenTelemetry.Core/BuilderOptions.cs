namespace Elastic.OpenTelemetry.Core;

internal readonly struct BuilderOptions<T> where T : class
{
	internal Action<T>? UserProvidedConfigureBuilder { get; init; }

	/// <summary>
	/// Used to indicate that the OTLP exporter should not be added by the current builder.
	/// A callee takes responsibility for adding it later when configuring the parent builder.
	/// </summary>
	internal bool DeferAddOtlpExporter { get; init; }
}
