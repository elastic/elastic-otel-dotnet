// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information
using OpenTelemetry;
using System.Diagnostics;

namespace Elastic.OpenTelemetry.Processors;

/// <summary>
///
/// </summary>
public class TransactionIdProcessor : BaseProcessor<Activity>
{
	/// <summary>
	/// 
	/// </summary>
    public const string TransactionIdTagName = "transaction.id";

    private readonly AsyncLocal<ActivitySpanId?> _currentTransactionId = new();

	/// <inheritdoc cref="OnStart"/>
    public override void OnStart(Activity activity)
    {
        if (activity.Parent == null)
            _currentTransactionId.Value = activity.SpanId;

        if (_currentTransactionId.Value.HasValue)
            activity.SetTag(TransactionIdTagName, _currentTransactionId.Value.Value.ToString());
    }
}
