// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.OpenTelemetry.Configuration;

internal enum ConfigSource
{
	Default, // Default value assigned within this class
	Environment, // Loaded from an environment variable
				 // ReSharper disable once InconsistentNaming
	IConfiguration, // Bound from an IConfiguration instance
	Property, // Set via property initializer
	Options // Set via user provided ElasticOpenTelemetryOptions
}
