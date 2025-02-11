// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using Elastic.OpenTelemetry.Configuration;

namespace Elastic.OpenTelemetry.Core;

internal sealed class BootstrapInfo(SdkActivationMethod activationMethod, StackTrace stackTrace, Exception? exception)
{
	public BootstrapInfo(SdkActivationMethod activationMethod, StackTrace stackTrace)
		: this(activationMethod, stackTrace, null) { }

	public BootstrapInfo(SdkActivationMethod activationMethod, Exception exception)
		: this(activationMethod, new StackTrace(exception), exception) { }

	public SdkActivationMethod ActivationMethod { get; } = activationMethod;

	public StackTrace StackTrace { get; } = stackTrace;

	public Exception? Exception { get; } = exception;

	public bool Succeeded => Exception is null;

	public bool Failed => !Succeeded;
}
