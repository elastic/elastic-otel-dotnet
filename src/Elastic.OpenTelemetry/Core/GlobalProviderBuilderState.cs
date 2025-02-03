// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.OpenTelemetry.Core;

internal sealed class GlobalProviderBuilderState
{
	private int _useElasticDefaultsCounter;

	public int IncrementUseElasticDefaults() =>
		Interlocked.Increment(ref _useElasticDefaultsCounter);

	public int UseElasticDefaultsCounter => _useElasticDefaultsCounter;
}
