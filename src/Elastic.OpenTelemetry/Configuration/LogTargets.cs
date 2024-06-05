// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.OpenTelemetry.Configuration;

/// <summary>
/// Control how the distribution should globally log.
/// </summary>
[Flags]
public enum LogTargets
{

	/// <summary>No global logging </summary>
	None,
	/// <summary>
	/// Enable file logging. Use <see cref="ElasticOpenTelemetryOptions.LogLevel"/>
	/// and <see cref="ElasticOpenTelemetryOptions.LogDirectoryDefault"/> to set any values other than the defaults
	/// </summary>
	File = 1 << 0, //1
	/// <summary>
	/// Write to standard out, useful in scenarios where file logging might not be an option or harder to set up.
	/// <para>e.g. containers, k8s, etc.</para>
	/// </summary>
	StdOut = 1 << 1, //2
}
