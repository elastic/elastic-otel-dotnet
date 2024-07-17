// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using NetEscapades.EnumGenerators;

namespace Elastic.OpenTelemetry.Configuration;

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
