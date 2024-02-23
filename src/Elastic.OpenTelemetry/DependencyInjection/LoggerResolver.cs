// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Elastic.OpenTelemetry.DependencyInjection;

internal sealed class LoggerResolver
{
	private static ILoggerFactory LoggerFactory = NullLoggerFactory.Instance;

	public LoggerResolver(ILoggerFactory loggerFactory)
	{
		if (LoggerFactory == NullLoggerFactory.Instance)
			LoggerFactory = loggerFactory;
	}

	internal static ILogger GetLogger<T>() => new ScopedCompositeLogger<T>(LoggerFactory.CreateLogger<T>());
}
