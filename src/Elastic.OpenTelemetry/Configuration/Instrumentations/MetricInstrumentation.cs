// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using NetEscapades.EnumGenerators;

namespace Elastic.OpenTelemetry.Configuration.Instrumentations;

/// <summary>
/// A hash set to enable <see cref="MetricInstrumentation"/> for auto-instrumentation.
/// </summary>
/// <remarks>
/// Explicitly enable specific <see cref="MetricInstrumentation"/> libraries.
/// </remarks>
internal class MetricInstrumentations(IEnumerable<MetricInstrumentation> instrumentations) : HashSet<MetricInstrumentation>(instrumentations)
{
	/// <summary>
	/// All available <see cref="MetricInstrumentation"/> libraries.
	/// </summary>
	public static readonly MetricInstrumentations All = new([.. MetricInstrumentationExtensions.GetValues()]);

	/// <inheritdoc cref="object.ToString"/>
	public override string ToString()
	{
		if (Count == 0)
			return "None";
		if (Count == All.Count)
			return "All";
		if (All.Count - Count < All.Count)
			return $"All Except: {string.Join(", ", All.Except(this).Select(i => i.ToStringFast()))}";

		return string.Join(", ", this.Select(i => i.ToStringFast()));
	}
}

/// <summary>
/// Available metric instrumentations.
/// </summary>
[EnumExtensions]
internal enum MetricInstrumentation
{
	/// <summary>ASP.NET Framework.</summary>
	AspNet,
	/// <summary>ASP.NET Core.</summary>
	AspNetCore,
	/// <summary>System.Net.Http.HttpClient and System.Net.HttpWebRequest metrics.</summary>
	HttpClient,
	/// <summary>OpenTelemetry.Instrumentation.Runtime metrics.</summary>
	NetRuntime,
	/// <summary>OpenTelemetry.Instrumentation.Process metrics.</summary>
	Process,
	/// <summary>NServiceBus metrics.</summary>
	NServiceBus
}
