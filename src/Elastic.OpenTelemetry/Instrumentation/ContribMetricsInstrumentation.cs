// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Core;

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
			AssemblyName = "OpenTelemetry.Instrumentation.AspNet",
			FullyQualifiedType = "OpenTelemetry.Metrics.MeterProviderBuilderExtensions",
			InstrumentationMethod = "AddAspNetInstrumentation"
		},

		new()
		{
			Name = "AspNetCore",
			AssemblyName = "OpenTelemetry.Instrumentation.AspNetCore",
			FullyQualifiedType = "OpenTelemetry.Metrics.AspNetCoreInstrumentationMeterProviderBuilderExtensions",
			InstrumentationMethod = "AddAspNetCoreInstrumentation"
		},

		new()
		{
			Name = "AWS",
			AssemblyName = "OpenTelemetry.Instrumentation.AWS",
			FullyQualifiedType = "OpenTelemetry.Metrics.MeterProviderBuilderExtensions",
			InstrumentationMethod = "AddAWSInstrumentation"
		},

		new()
		{
			Name = "Cassandra",
			AssemblyName = "OpenTelemetry.Instrumentation.Cassandra",
			FullyQualifiedType = "OpenTelemetry.Metrics.MeterProviderBuilderExtensions",
			InstrumentationMethod = "AddCassandraInstrumentation"
		},

		new()
		{
			Name = "Kafka (Producer)",
			AssemblyName = "OpenTelemetry.Instrumentation.ConfluentKafka",
			FullyQualifiedType = "OpenTelemetry.Metrics.MeterProviderBuilderExtensions",
			InstrumentationMethod = "AddKafkaProducerInstrumentation"
		},

		new()
		{
			Name = "Kafka (Consumer)",
			AssemblyName = "OpenTelemetry.Instrumentation.ConfluentKafka",
			FullyQualifiedType = "OpenTelemetry.Metrics.MeterProviderBuilderExtensions",
			InstrumentationMethod = "AddKafkaConsumerInstrumentation"
		},

		new()
		{
			Name = "EventCounters",
			AssemblyName = "OpenTelemetry.Instrumentation.EventCounters",
			FullyQualifiedType = "OpenTelemetry.Metrics.MeterProviderBuilderExtensions",
			InstrumentationMethod = "AddEventCountersInstrumentation"
		},

		new()
		{
			Name = "HTTP",
			AssemblyName = "OpenTelemetry.Instrumentation.Http",
			FullyQualifiedType = "OpenTelemetry.Metrics.HttpClientInstrumentationMeterProviderBuilderExtensions",
			InstrumentationMethod = "AddHttpClientInstrumentation"
		},

		new()
		{
			Name = "Runtime",
			AssemblyName = "OpenTelemetry.Instrumentation.Runtime",
			FullyQualifiedType = "OpenTelemetry.Metrics.MeterProviderBuilderExtensions",
			InstrumentationMethod = "AddRuntimeInstrumentation"
		},

		new()
		{
			Name = "Process",
			AssemblyName = "OpenTelemetry.Instrumentation.Process",
			FullyQualifiedType = "OpenTelemetry.Metrics.MeterProviderBuilderExtensions",
			InstrumentationMethod = "AddProcessInstrumentation"
		}
	];
}
