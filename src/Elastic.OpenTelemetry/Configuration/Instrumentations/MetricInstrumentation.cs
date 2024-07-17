// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using NetEscapades.EnumGenerators;

namespace Elastic.OpenTelemetry.Configuration.Instrumentations;

/// <summary> A hash set to enable <see cref="MetricInstrumentation"/></summary>
public class MetricInstrumentations : HashSet<MetricInstrumentation>
{
	/// <summary> All available <see cref="MetricInstrumentation"/> </summary>
	public static readonly MetricInstrumentations All = new([..MetricInstrumentationExtensions.GetValues()]);

	/// <summary> Explicitly enable specific <see cref="TraceInstrumentation"/> </summary>
	public MetricInstrumentations(IEnumerable<MetricInstrumentation> instrumentations) : base(instrumentations) { }

	/// <inheritdoc cref="object.ToString"/>
	public override string ToString()
	{
		if (Count == 0) return "None";
		if (Count == All.Count) return "All";
		if (All.Count - Count < 5)
			return $"All Except: {string.Join(", ", All.Except(this).Select(i => i.ToStringFast()))}";
		return string.Join(", ", this.Select(i => i.ToStringFast()));
	}
}

/// <summary> Available metric instrumentations. </summary>
[EnumExtensions]
public enum MetricInstrumentation
{
	///<summary>ASP.NET Framework</summary>
	AspNet,
	///<summary>ASP.NET Core</summary>
	AspNetCore,
	///<summary>System.Net.Http.HttpClient and System.Net.HttpWebRequest,	HttpClient metrics	</summary>
	HttpClient,
	///<summary>OpenTelemetry.Instrumentation.Runtime,	Runtime metrics	</summary>
	NetRuntime,
	///<summary>OpenTelemetry.Instrumentation.Process,Process metrics	</summary>
	Process,
	///<summary>NServiceBus metrics</summary>
	NServiceBus
}
