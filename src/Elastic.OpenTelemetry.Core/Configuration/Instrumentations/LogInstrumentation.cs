// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using NetEscapades.EnumGenerators;

namespace Elastic.OpenTelemetry.Configuration.Instrumentations;

/// <summary>
/// A hash set to enable <see cref="LogInstrumentation"/> for auto-instrumentation.
/// </summary>
/// <remarks>
/// Explicitly enable specific <see cref="LogInstrumentation"/> libraries.
/// </remarks>
internal class LogInstrumentations(IEnumerable<LogInstrumentation> instrumentations) : HashSet<LogInstrumentation>(instrumentations)
{
	/// <summary>
	/// All available <see cref="LogInstrumentation"/> libraries.
	/// </summary>
	public static readonly LogInstrumentations All = new([.. LogInstrumentationExtensions.GetValues()]);

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
/// Available logging instrumentations.
/// </summary>
[EnumExtensions]
internal enum LogInstrumentation
{
	/// <summary>ILogger instrumentation.</summary>
	// ReSharper disable once InconsistentNaming
	ILogger
}
