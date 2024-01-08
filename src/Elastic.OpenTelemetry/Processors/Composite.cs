// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information
namespace Elastic.OpenTelemetry.Processors;

// TODO - Consider a struct, but consider if this would get copied too much
internal class Composite
{
    /// <summary>
    /// A string value indicating which compression strategy was used. The valid values are `exact_match` and `same_kind`
    /// </summary>
    public string CompressionStrategy { get; set; } = "exact_match";

    /// <summary>
    /// Count is the number of compressed spans the composite span represents. The minimum count is 2, as a composite span represents at least two spans.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Sum of the durations of all compressed spans this composite span represents in milliseconds.
    /// </summary>
    public double DurationSum { get; set; }
}
