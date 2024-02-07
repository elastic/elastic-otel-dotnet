// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry;

namespace Elastic.OpenTelemetry.Processors;

/// <summary>
/// 
/// </summary>
public abstract class ElasticProcessor<T> : BaseProcessor<Activity>,  IElasticProcessor
{
	/// <summary>
	/// 
	/// </summary>
	protected static ILogger Logger { get; private set; } = NullLogger.Instance;

	void IElasticProcessor.Initialize(IServiceProvider serviceProvider)
	{
		var resolvedLogger = serviceProvider.GetService<ILogger<T>>();

		if (resolvedLogger is ILogger logger)
		{
			Logger = logger;
		}

		Logger.LogInformation("Initialised {ProcessorType}.", typeof(T));
	}
}
