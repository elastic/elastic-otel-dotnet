// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information
using OpenTelemetry.Trace;

namespace Elastic.OpenTelemetry.AspNetCore;

/// <summary>
/// 
/// </summary>
public static class AgentBuilderExtensions
{
	/// <summary>
	/// 
	/// </summary>
	/// <param name="agentBuilder"></param>
	public static AgentBuilder AddAspNetCore(this AgentBuilder agentBuilder) => agentBuilder.ConfigureTracer(tpb => tpb.AddAspNetCoreInstrumentation());
}
