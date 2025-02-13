// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using Elastic.OpenTelemetry.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
public sealed class ElasticCompatibilityProcessor(ILogger? logger) : BaseProcessor<Activity>
{
	private readonly ILogger _logger = logger ?? NullLogger.Instance;

	/// <inheritdoc />
	public override void OnEnd(Activity activity)
	{
		if (activity.Kind == ActivityKind.Server)
		{
			// For inbound HTTP requests (server), ASP.NET Core sets the newer semantic conventions in
			// the latest versions. For now, we need to ensure the older semantic conventions are also
			// included on the spans sent to the Elastic backend as the intake system is currently
			// unaware of the newer semantic conventions. We send the older attributes to ensure that
			// the UI functions as expected. The http and net host conventions are required to build
			// up the URL displayed in the trace sample UI within Kibana. This will be fixed in future
			// version of apm-data.

			string? httpScheme = null;
			string? httpTarget = null;
			string? urlScheme = null;
			string? urlPath = null;
			string? urlQuery = null;
			string? netHostName = null;
			int? netHostPort = null;
			string? serverAddress = null;
			int? serverPort = null;

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
					netHostPort = ProcessIntAttribute(tag);

				if (tag.Key == ServerPort)
					serverPort = ProcessIntAttribute(tag);
			}

			// Set the older semantic convention attributes
			if (httpScheme is null && urlScheme is not null)
				SetStringAttribute(HttpScheme, urlScheme);

			if (httpTarget is null && urlPath is not null)
			{
				var target = urlPath;

				if (urlQuery is not null)
					target += urlQuery;

				SetStringAttribute(HttpTarget, target);
			}

			if (netHostName is null && serverAddress is not null)
				SetStringAttribute(NetHostName, serverAddress);

			if (netHostPort is null && serverPort is not null)
				SetIntAttribute(NetHostPort, serverPort.Value);
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

		int? ProcessIntAttribute(KeyValuePair<string, object?> tag)
		{
			if (tag.Value is int value)
			{
				_logger.FoundTag(nameof(ElasticCompatibilityProcessor), tag.Key, value);
				return value;
			}

			return null;
		}

		void SetStringAttribute(string attributeName, string value)
		{
			_logger.SetTag(nameof(ElasticCompatibilityProcessor), attributeName, value);
			activity.SetTag(attributeName, value);
		}

		void SetIntAttribute(string attributeName, int value)
		{
			_logger.SetTag(nameof(ElasticCompatibilityProcessor), attributeName, value);
			activity.SetTag(attributeName, value);
		}
	}
}
