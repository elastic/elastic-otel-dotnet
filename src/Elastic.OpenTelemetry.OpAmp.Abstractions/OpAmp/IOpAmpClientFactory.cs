// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.OpAmp.Abstractions;

/// <summary>
/// Factory for creating <see cref="IOpAmpClient"/> instances.
/// </summary>
/// <remarks>
/// This interface exists to provide a compile-time contract between
/// <c>CentralConfiguration</c> (which calls the factory) and
/// <c>ElasticOpAmpClient</c> (whose constructor must match).
/// The ALC isolation path activates the factory via reflection using a
/// parameterless constructor, then calls <see cref="Create"/> — keeping
/// the reflection surface trivial and the parameter contract compiler-checked.
/// </remarks>
internal interface IOpAmpClientFactory
{
	IOpAmpClient Create(ILogger logger, string endPoint, string headers,
		string serviceName, string? serviceVersion, string userAgent);
}
