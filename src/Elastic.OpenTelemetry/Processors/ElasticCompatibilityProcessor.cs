// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using Elastic.OpenTelemetry.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using static Elastic.OpenTelemetry.SemanticConventions.TraceSemanticConventions;

namespace Elastic.OpenTelemetry.Processors;

/// <summary>
/// This processor ensures that the data is compatible with Elastic backends.
/// <para>
/// It checks for the presence of the older semantic conventions and if they are not present, it will
/// add them. This is only necessary for compatibility with older versions of the intake OTel endpoints
/// on Elastic APM. These issues will be fixed centrally in future versions of the intake code.
/// </para>
/// </summary>
/// <param name="logger"></param>
public class ElasticCompatibilityProcessor(ILogger logger) : BaseProcessor<Activity>
{
	private readonly ILogger _logger = logger;

	/// <inheritdoc />
	public override void OnEnd(Activity activity)
	{
		if (activity.Kind == ActivityKind.Server)
		{
			string? httpScheme = null;
			string? httpTarget = null;
			string? urlScheme = null;
			string? urlPath = null;
			string? urlQuery = null;
			string? netHostName = null;
			string? netHostPort = null;
			string? serverAddress = null;
			string? serverPort = null;

			// We loop once, collecting all the attributes we need for the older and newer
			// semantic conventions. This is a bit more verbose but ensures we don't iterate
			// the tags multiple times.
			foreach (var tag in activity.TagObjects)
			{
				if (tag.Key == HttpScheme)
					httpScheme = ProcessStringAttribute(tag);

				if (tag.Key == HttpTarget)
					httpTarget = ProcessStringAttribute(tag);

				if (tag.Key == UrlScheme)
					urlScheme = ProcessStringAttribute(tag);

				if (tag.Key == UrlPath)
					urlPath = ProcessStringAttribute(tag);

				if (tag.Key == UrlQuery)
					urlQuery = ProcessStringAttribute(tag);

				if (tag.Key == NetHostName)
					netHostName = ProcessStringAttribute(tag);

				if (tag.Key == ServerAddress)
					serverAddress = ProcessStringAttribute(tag);

				if (tag.Key == NetHostPort)
					netHostPort = ProcessStringAttribute(tag);

				if (tag.Key == ServerPort)
					serverPort = ProcessStringAttribute(tag);
			}

			// Set the older semantic convention attributes
			if (httpScheme is null && urlScheme is not null)
				SetAttribute(HttpScheme, urlScheme);

			if (httpTarget is null && urlPath is not null)
			{
				var target = urlPath;

				if (urlQuery is not null)
					target += $"?{urlQuery}";

				SetAttribute(HttpTarget, target);
			}

			if (netHostName is null && serverAddress is not null)
				SetAttribute(NetHostName, serverAddress);

			if (netHostPort is null && serverPort is not null)
				SetAttribute(NetHostPort, serverPort);
		}

		string? ProcessStringAttribute(KeyValuePair<string, object?> tag)
		{
			if (tag.Value is string value)
			{
				_logger.FoundTag(nameof(ElasticCompatibilityProcessor), tag.Key, value);
				return value;
			}

			return null;
		}

		void SetAttribute(string attributeName, string value)
		{
			_logger.SetTag(nameof(ElasticCompatibilityProcessor), attributeName, value);
			activity.SetTag(attributeName, value);
		}
	}
}
