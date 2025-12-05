// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Diagnostics;
using Microsoft.Extensions.Logging;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Elastic.OpenTelemetry.Core;
#pragma warning restore IDE0130 // Namespace does not match folder structure

internal static class LoggerFactoryExtensions
{
	internal static ILogger CreateElasticLogger(this ILoggerFactory loggerFactory) =>
		loggerFactory.CreateLogger(CompositeLogger.LogCategory);
}
