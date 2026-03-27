// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.OpAmp.Abstractions;
using Microsoft.Extensions.Logging;

namespace Elastic.OpenTelemetry.OpAmp
{
	/// <summary>
	/// Factory that creates <see cref="ElasticOpAmpClient"/> instances.
	/// </summary>
	/// <remarks>
	/// WARNING: This class is activated by reflection from
	/// <c>OpAmpIsolatedLoadContext.CreateOpAmpClientInstance</c>.
	/// The parameterless constructor and <see cref="IOpAmpClientFactory"/> implementation
	/// are both required for the ALC path to function.
	/// </remarks>
	internal sealed class ElasticOpAmpClientFactory : IOpAmpClientFactory
	{
		public IOpAmpClient Create(ILogger logger, string endPoint, string headers,
			string serviceName, string? serviceVersion, string userAgent)
			=> new ElasticOpAmpClient(logger, endPoint, headers, serviceName, serviceVersion, userAgent);
	}
}
