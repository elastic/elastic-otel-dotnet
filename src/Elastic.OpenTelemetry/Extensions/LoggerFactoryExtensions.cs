// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry;

internal static class LoggerFactoryExtensions
{
	public static ILogger CreateElasticLogger(this ILoggerFactory loggerFactory) =>
		loggerFactory.CreateLogger(CompositeLogger.LogCategory);
}
