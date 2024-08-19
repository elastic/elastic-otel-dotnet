// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using NetEscapades.EnumGenerators;

namespace Elastic.OpenTelemetry.Configuration;

/// <summary> Observability signals to enable, defaults to all. </summary>
[Flags]
[EnumExtensions]
public enum Signals
{
	/// <summary> No Elastic defaults will be included, acting effectively as a vanilla OpenTelemetry </summary>
	None,

	/// <summary> Include Elastic Distribution of OpenTelemetry .NET tracing defaults</summary>
	Traces = 1 << 0, //1

	/// <summary> Include Elastic Distribution of OpenTelemetry .NET metrics defaults</summary>
	Metrics = 1 << 1, //2

	/// <summary> Include Elastic Distribution of OpenTelemetry .NET logging defaults</summary>
	Logs = 1 << 2, //4

	/// <summary> (default) Include all Elastic Distribution of OpenTelemetry .NET logging defaults</summary>
	All = ~0
}
