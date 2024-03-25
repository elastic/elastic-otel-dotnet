// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information
using System.Diagnostics;
using Elastic.OpenTelemetry.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenTelemetry;

namespace Elastic.OpenTelemetry.Processors;

/// <summary>
///
/// </summary>
public sealed class TransactionIdProcessor(ILogger logger) : BaseProcessor<Activity>
{
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

		if (!_currentTransactionId.Value.HasValue)
			return;

		activity.SetTag(TransactionIdTagName, _currentTransactionId.Value.Value.ToString());
		logger.TransactionIdProcessorTagAdded();
	}
}
