// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information
using System.Diagnostics;
using Elastic.OpenTelemetry.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenTelemetry;

using static Elastic.OpenTelemetry.Diagnostics.ElasticOpenTelemetryDiagnostics;

namespace Elastic.OpenTelemetry.Processors;

/// <summary>
///
/// </summary>
public class TransactionIdProcessor : BaseProcessor<Activity>
{
	private ILogger? _logger = null;
	private readonly AsyncLocal<ActivitySpanId?> _currentTransactionId = new();

	/// <summary>
	/// 
	/// </summary>
	public const string TransactionIdTagName = "transaction.id";

	/// <inheritdoc />
	public override void OnStart(Activity activity)
	{
		if (activity.Parent == null)
			_currentTransactionId.Value = activity.SpanId;

		if (_currentTransactionId.Value.HasValue)
		{
			activity.SetTag(TransactionIdTagName, _currentTransactionId.Value.Value.ToString());
			Log(TransactionIdAddedEvent, () => DiagnosticEvent.Create<TransactionIdProcessor>(ref _logger, activity));
		}
	}
}
