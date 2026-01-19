// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.OpAmp.Abstractions;

/// <summary>
/// Factory for creating OpAmp message subscribers that are isolated from version conflicts.
/// This factory is called from the isolated AssemblyLoadContext and returns instances
/// that communicate through primitive-only interfaces.
/// </summary>
public static class OpAmpMessageSubscriberFactory
{
	/// <summary>
	/// Creates an OpAmp message subscriber.
	/// </summary>
	public static IOpAmpMessageSubscriber Create(ILogger logger) => new OpAmpMessageSubscriberImpl(logger);
}
