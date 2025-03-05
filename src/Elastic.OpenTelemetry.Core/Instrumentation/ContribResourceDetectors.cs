// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Core;

namespace Elastic.OpenTelemetry.Instrumentation;

internal static class ContribResourceDetectors
{
	// Note: This is defined as a static method and allocates the array each time.
	// This is intentional, as we expect this to be invoked once (or worst case, few times).
	// After initialisation, the array is no longer required and can be reclaimed by the GC.
	// This is likley to be overall more efficient for the common scenario as we don't keep
	// an object alive for the lifetime of the application.
	public static InstrumentationAssemblyInfo[] GetContribResourceDetectors() =>
	[
		new()
		{
			Name = "HostDetector",
			Filename = "OpenTelemetry.Resources.Host.dll",
			FullyQualifiedType = "OpenTelemetry.Resources.HostResourceBuilderExtensions",
			InstrumentationMethod = "AddHostDetector"
		}
	];
}
