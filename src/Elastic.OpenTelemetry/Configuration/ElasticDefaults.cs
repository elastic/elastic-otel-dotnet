// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.OpenTelemetry.Configuration;

/// <summary>
/// Control which elastic defaults you want to include.
/// <para>NOTE: this is an expert level option only use this if you want to take full control of the OTEL configuration</para>
/// <para>defaults to <see cref="ElasticDefaults.All"/></para>
/// </summary>
[Flags]
public enum ElasticDefaults
{
	/// <summary> No Elastic defaults will be included, acting effectively as a vanilla OpenTelemetry </summary>
	None,

	/// <summary> Include Elastic Distribution for OpenTelemetry .NET tracing defaults</summary>
	Traces = 1 << 0, //1

	/// <summary> Include Elastic Distribution for OpenTelemetry .NET metrics defaults</summary>
	Metrics = 1 << 1, //2

	/// <summary> Include Elastic Distribution for OpenTelemetry .NET logging defaults</summary>
	Logs = 1 << 2, //4

	/// <summary> (default) Include all Elastic Distribution for OpenTelemetry .NET logging defaults</summary>
	All = ~0
}
