// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using NetEscapades.EnumGenerators;

namespace Elastic.OpenTelemetry.Configuration;

/// <summary> Available logs instrumentations. </summary>
[EnumExtensions]
public enum LogInstrumentation
{
	/// <summary> ILogger instrumentation</summary>
	// ReSharper disable once InconsistentNaming
	ILogger
}
