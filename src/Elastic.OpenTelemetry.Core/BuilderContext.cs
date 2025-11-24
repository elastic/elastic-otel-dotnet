// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Microsoft.Extensions.DependencyInjection;

namespace Elastic.OpenTelemetry.Core;

/// <summary>
/// This is used internally to pass context around during the fluent builder configuration process.
/// </summary>
internal sealed class BuilderContext<T> where T : class
{
	internal required T Builder { get; init; }

	internal required BuilderState BuilderState { get; init; }

	internal required BuilderOptions<T> BuilderOptions { get; init; }

	internal IServiceCollection? Services { get; init; }
}
