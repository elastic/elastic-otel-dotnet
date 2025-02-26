// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.OpenTelemetry.Core;

/// <summary>
/// Used to store bootstrap information and a single component instance that will later be
/// tracked per builder (OpenTelemetryBuilder, TracerProviderBuilder, MeterProviderBuilder
/// or LoggerProviderBuilder) instance.
/// </summary>
internal sealed class BuilderState(
	BootstrapInfo bootstrapInfo,
	ElasticOpenTelemetryComponents components,
	Guid? instanceIdentifier = null)
{
	private int _useElasticDefaultsCounter;

	public BootstrapInfo BootstrapInfo { get; } = bootstrapInfo;

	public ElasticOpenTelemetryComponents Components { get; } = components;

	public Guid InstanceIdentifier { get; } = instanceIdentifier ?? Guid.NewGuid();

	public void IncrementWithElasticDefaults() =>
		Interlocked.Increment(ref _useElasticDefaultsCounter);

	public int WithElasticDefaultsCounter => _useElasticDefaultsCounter;
}
