// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Core;

namespace Elastic.OpenTelemetry.Instrumentation;

internal static class ContribResourceDetectors
{
	// Note: We do not currently attempt to automatically register detectors for cloud
	// environments such as AWS, Azure and GCP because each have several different
	// deployment scenarios with related extension methods. Therefore, it is best for the
	// consumer to register those explicitly.

	// Note: This is defined as a static method and allocates the array each time.
	// This is intentional, as we expect this to be invoked once (or worst case, few times).
	// After initialisation, the array is no longer required and can be reclaimed by the GC.
	// This is likley to be overall more efficient for the common scenario as we don't keep
	// an object alive for the lifetime of the application.
	public static InstrumentationAssemblyInfo[] GetContribResourceDetectors() =>
	[
		new()
		{
			Name = "Container",
			AssemblyName = "OpenTelemetry.Resources.Container",
			FullyQualifiedType = "OpenTelemetry.Resources.ContainerResourceBuilderExtensions",
			InstrumentationMethod = "AddContainerDetector"
		},

		new()
		{
			Name = "OperatingSystem",
			AssemblyName = "OpenTelemetry.Resources.OperatingSystem",
			FullyQualifiedType = "OpenTelemetry.Resources.OperatingSystemResourceBuilderExtensions",
			InstrumentationMethod = "AddOperatingSystemDetector"
		},

		new()
		{
			Name = "Process",
			AssemblyName = "OpenTelemetry.Resources.Process",
			FullyQualifiedType = "OpenTelemetry.Resources.ProcessResourceBuilderExtensions",
			InstrumentationMethod = "AddProcessDetector"
		}
	];
}
