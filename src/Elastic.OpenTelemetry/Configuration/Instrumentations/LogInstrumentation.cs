// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using NetEscapades.EnumGenerators;

namespace Elastic.OpenTelemetry.Configuration.Instrumentations;

/// <summary> A hash set to enable <see cref="LogInstrumentation"/></summary>
public class LogInstrumentations : HashSet<LogInstrumentation>
{
	/// <summary> All available <see cref="LogInstrumentation"/> </summary>
	public static readonly LogInstrumentations All = new([..LogInstrumentationExtensions.GetValues()]);

	/// <summary> Explicitly enable specific <see cref="TraceInstrumentation"/> </summary>
	public LogInstrumentations(IEnumerable<LogInstrumentation> instrumentations) : base(instrumentations) { }

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

/// <summary> Available logs instrumentations. </summary>
[EnumExtensions]
public enum LogInstrumentation
{
	/// <summary> ILogger instrumentation</summary>
	// ReSharper disable once InconsistentNaming
	ILogger
}
