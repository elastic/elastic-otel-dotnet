// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.OpenTelemetry.Instrumentation;

internal static class ContribMetricsInstrumentation
{
	// Note: This is defined as a static method and allocates the array each time.
	// This is intentional, as we expect this to be invoked once (or worst case, few times).
	// After initialisation, the array is no longer required and can be reclaimed by the GC.
	// This is likley to be overall more efficient for the common scenario as we don't keep
	// an object alive for the lifetime of the application.
	public static InstrumentationAssemblyInfo[] GetMetricsInstrumentationAssembliesInfo() =>
	[
		new()
		{
			Name = "AspNet",
			Filename = "OpenTelemetry.Instrumentation.AspNet.dll",
			FullyQualifiedType = "OpenTelemetry.Metrics.MeterProviderBuilderExtensions",
			InstrumentationMethod = "AddAspNetInstrumentation"
		},

		new()
		{
			Name = "AspNetCore",
			Filename = "OpenTelemetry.Instrumentation.AspNetCore.dll",
			FullyQualifiedType = "OpenTelemetry.Metrics.AspNetCoreInstrumentationMeterProviderBuilderExtensions",
			InstrumentationMethod = "AddAspNetCoreInstrumentation"
		},

		new()
		{
			Name = "AWS",
			Filename = "OpenTelemetry.Instrumentation.AWS.dll",
			FullyQualifiedType = "OpenTelemetry.Metrics.MeterProviderBuilderExtensions",
			InstrumentationMethod = "AddAWSInstrumentation"
		},

		new()
		{
			Name = "Cassandra",
			Filename = "OpenTelemetry.Instrumentation.Cassandra.dll",
			FullyQualifiedType = "OpenTelemetry.Metrics.MeterProviderBuilderExtensions",
			InstrumentationMethod = "AddCassandraInstrumentation"
		},

		new()
		{
			Name = "Kafka (Producer)",
			Filename = "OpenTelemetry.Instrumentation.ConfluentKafka.dll",
			FullyQualifiedType = "OpenTelemetry.Metrics.MeterProviderBuilderExtensions",
			InstrumentationMethod = "AddKafkaProducerInstrumentation"
		},

		new()
		{
			Name = "Kafka (Consumer)",
			Filename = "OpenTelemetry.Instrumentation.ConfluentKafka.dll",
			FullyQualifiedType = "OpenTelemetry.Metrics.MeterProviderBuilderExtensions",
			InstrumentationMethod = "AddKafkaConsumerInstrumentation"
		},

		new()
		{
			Name = "EventCounters",
			Filename = "OpenTelemetry.Instrumentation.EventCounters.dll",
			FullyQualifiedType = "OpenTelemetry.Metrics.MeterProviderBuilderExtensions",
			InstrumentationMethod = "AddEventCountersInstrumentation"
		},

#if NET9_0_OR_GREATER
		// On .NET 9, we add the `System.Net.Http` source for native instrumentation, rather than referencing
		// the contrib instrumentation. However, if the consuming application has their own reference to
		// `OpenTelemetry.Instrumentation.Http`, then we use that since it signals the consumer prefers the
		// contrib instrumentation. Therefore, on .NET 9+ targets, we attempt to dynamically load the contrib
		// instrumentation, when available.
		new()
		{
			Name = "Http",
			Filename = "OpenTelemetry.Instrumentation.Http.dll",
			FullyQualifiedType = "OpenTelemetry.Metrics.HttpClientInstrumentationMeterProviderBuilderExtensions",
			InstrumentationMethod = "AddHttpClientInstrumentation"
		},
#endif
	];
}
