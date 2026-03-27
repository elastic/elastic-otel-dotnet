// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.OpenTelemetry.OpAmp.Abstractions
{
	internal sealed class ElasticRemoteConfig
	{
		public ElasticRemoteConfig(string? logLevel) => LogLevel = logLevel;

		public string? LogLevel { get; }
	}
}
