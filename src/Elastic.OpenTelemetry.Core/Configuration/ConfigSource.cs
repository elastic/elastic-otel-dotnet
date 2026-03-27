// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.OpenTelemetry.Configuration;

/// <summary>
/// Identifies the source of a configuration value. Ordinal values define precedence —
/// a higher value always wins over a lower value. Central configuration is authoritative
/// and overrides all other sources.
/// </summary>
internal enum ConfigSource
{
	/// <summary>Default value assigned within the options class.</summary>
	Default = 0,
	// ReSharper disable once InconsistentNaming
	/// <summary>Bound from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> instance.</summary>
	IConfiguration = 1,
	/// <summary>Loaded from an environment variable.</summary>
	Environment = 2,
	/// <summary>Set via user-provided <see cref="ElasticOpenTelemetryOptions"/>.</summary>
	Options = 3,
	/// <summary>Set via property initializer in code.</summary>
	Property = 4,
	/// <summary>Set via central configuration (OpAMP). Always takes highest precedence.</summary>
	CentralConfig = 5
}
