// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Elastic.OpenTelemetry.Instrumentation;
#pragma warning restore IDE0130 // Namespace does not match folder structure

internal readonly struct InstrumentationAssemblyInfo
{
	public readonly required string Name { get; init; }
	public readonly required string Filename { get; init; }
	public readonly required string FullyQualifiedType { get; init; }
	public readonly required string InstrumentationMethod { get; init; }
}
