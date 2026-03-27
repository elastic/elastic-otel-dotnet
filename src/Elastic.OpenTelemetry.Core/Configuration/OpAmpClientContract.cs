// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

#if NETFRAMEWORK || NET

namespace Elastic.OpenTelemetry.Core.Configuration;

/// <summary>
/// Constants for the reflection-based activation of the OpAmp client factory.
/// </summary>
/// <remarks>
/// These must match the actual types in the <c>Elastic.OpenTelemetry.OpAmp</c> assembly.
/// Mismatches are caught by the <c>FactoryTypeName_MatchesContract</c> and
/// <c>Factory_HasParameterlessConstructor</c> unit tests.
/// </remarks>
internal static class OpAmpClientContract
{
	internal const string AssemblyName = "Elastic.OpenTelemetry.OpAmp";
	internal const string FactoryTypeName = "Elastic.OpenTelemetry.OpAmp.ElasticOpAmpClientFactory";

	// Assembly names loaded into the isolated ALC for version isolation.
	internal const string ProtobufAssemblyName = "Google.Protobuf";
	internal const string OpAmpClientAssemblyName = "OpenTelemetry.OpAmp.Client";
}

#endif
