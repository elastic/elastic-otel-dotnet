// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information
using System.Diagnostics;
using Elastic.OpenTelemetry.DependencyInjection;
using Elastic.OpenTelemetry.Diagnostics;
using Elastic.OpenTelemetry.Diagnostics.Logging;
using Microsoft.Extensions.Logging;
using OpenTelemetry;

using static Elastic.OpenTelemetry.Diagnostics.ElasticOpenTelemetryDiagnostics;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Elastic.OpenTelemetry.Processors;

/// <summary>
///
/// </summary>
public class TransactionIdProcessor : BaseProcessor<Activity>
{
	private readonly AsyncLocal<ActivitySpanId?> _currentTransactionId = new();
	private readonly ILogger _logger = LoggerResolver.GetLogger<TransactionIdProcessor>();

	/// <summary>
	///
	/// </summary>
	public const string TransactionIdTagName = "transaction.id";

	/// <inheritdoc />
	public override void OnStart(Activity activity)
	{
		if (activity.Parent == null)
			_currentTransactionId.Value = activity.SpanId;

		if (!_currentTransactionId.Value.HasValue) return;

		activity.SetTag(TransactionIdTagName, _currentTransactionId.Value.Value.ToString());
		_logger.TransactionIdProcessorTagAdded();
	}
}
