// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information
using System.Diagnostics;

namespace Elastic.OpenTelemetry.Diagnostics;

internal sealed class LoggingDiagnosticSourceListener(LogFileWriter logFileWriter) : IObserver<DiagnosticListener>
{
	private readonly object _lock = new();
	private readonly LogFileWriter _logFileWriter = logFileWriter;

	public void OnNext(DiagnosticListener listener)
	{ 
		if (listener.Name == ElasticOpenTelemetryDiagnosticSource.DiagnosticSourceName)
		{
			lock (_lock)
				listener.Subscribe(new ElasticDiagnosticLoggingObserver(_logFileWriter));
		}
	}

	public void OnCompleted() { }

	public void OnError(Exception error) { }
}

