// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.OpenTelemetry.Core;

/// <summary>
/// Each XyzProviderBuilder (e.g. TracerProviderBuilder) uses a shared instance
/// of this to track the number of calls made to their `WithElasticDefaults` methods.
/// Generally, we expect only a single call. While we don't prohibit multiple calls,
/// by tracking the actual number, we can ensure we log this to enhance diagnostics
/// and support later on.
/// </summary>
internal sealed class GlobalProviderBuilderState
{
	private int _useElasticDefaultsCounter;

	public int IncrementWithElasticDefaults() =>
		Interlocked.Increment(ref _useElasticDefaultsCounter);

	public int WithElasticDefaultsCounter => _useElasticDefaultsCounter;
}
