// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information
using System.Diagnostics;
using OpenTelemetry;

namespace Elastic.OpenTelemetry.Processors;

/// <summary> A processor that includes stack trace information of long running span </summary>
public class StackTraceProcessor : BaseProcessor<Activity>
{
	/// <inheritdoc cref="OnStart"/>
	public override void OnStart(Activity data)
	{
		//for now always capture stack trace on start
		var stackTrace = new StackTrace(true);
		data.SetCustomProperty("_stack_trace", stackTrace);
		base.OnStart(data);
	}

	/// <inheritdoc cref="OnEnd"/>
	public override void OnEnd(Activity data)
	{
		if (data.GetCustomProperty("_stack_trace") is not StackTrace stackTrace)
			return;
		if (data.Duration < TimeSpan.FromMilliseconds(2))
			return;

		data.SetTag("code.stacktrace", stackTrace);
		base.OnEnd(data);
	}
}
