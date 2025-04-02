// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Core;

namespace Elastic.OpenTelemetry.Instrumentation;

internal static class ContribTraceInstrumentation
{
	// Note: This is defined as a static method and allocates the array each time.
	// This is intentional, as we expect this to be invoked once (or worst case, few times).
	// After initialisation, the array is no longer required and can be reclaimed by the GC.
	// This is likley to be overall more efficient for the common scenario as we don't keep
	// an object alive for the lifetime of the application.
	public static InstrumentationAssemblyInfo[] GetReflectionInstrumentationAssemblies() =>
	[
		new()
		{
			Name = "AspNet",
			AssemblyName = "OpenTelemetry.Instrumentation.AspNet",
			FullyQualifiedType = "OpenTelemetry.Trace.TracerProviderBuilderExtensions",
			InstrumentationMethod = "AddAspNetInstrumentation"
		},

		// NOTE: We don't add ASP.NET Core here as we special-case it and handle it manually

		new()
		{
			Name = "AWS",
			AssemblyName = "OpenTelemetry.Instrumentation.AWS",
			FullyQualifiedType = "OpenTelemetry.Trace.TracerProviderBuilderExtensions",
			InstrumentationMethod = "AddAWSInstrumentation"
		},

		new()
		{
			Name = "ElasticsearchClient (NEST)",
			AssemblyName = "OpenTelemetry.Instrumentation.ElasticsearchClient",
			FullyQualifiedType = "OpenTelemetry.Trace.TracerProviderBuilderExtensions",
			InstrumentationMethod = "AddElasticsearchClientInstrumentation"
		},

		new()
		{
			Name = "EntityFrameworkCore",
			AssemblyName = "OpenTelemetry.Instrumentation.EntityFrameworkCore",
			FullyQualifiedType = "OpenTelemetry.Trace.TracerProviderBuilderExtensions",
			InstrumentationMethod = "AddEntityFrameworkCoreInstrumentation"
		},

		new()
		{
			Name = "GrpcNetClient",
			AssemblyName = "OpenTelemetry.Instrumentation.GrpcNetClient",
			FullyQualifiedType = "OpenTelemetry.Trace.TracerProviderBuilderExtensions",
			InstrumentationMethod = "AddGrpcClientInstrumentation"
		},

		new()
		{
			Name = "GrpcCore",
			AssemblyName = "OpenTelemetry.Instrumentation.GrpcCore",
			FullyQualifiedType = "OpenTelemetry.Trace.TracerProviderBuilderExtensions",
			InstrumentationMethod = "AddGrpcCoreInstrumentation"
		},

		new()
		{
			Name = "Hangfire",
			AssemblyName = "OpenTelemetry.Instrumentation.Hangfire",
			FullyQualifiedType = "OpenTelemetry.Trace.TracerProviderBuilderExtensions",
			InstrumentationMethod = "AddHangfireInstrumentation"
		},

		// On .NET 9, we add the `System.Net.Http` source for native instrumentation, rather than referencing
		// the contrib instrumentation. However, if the consuming application has their own reference to
		// `OpenTelemetry.Instrumentation.Http`, then we use that since it signals the consumer prefers the
		// contrib instrumentation. Therefore, even on .NET 9+ targets, we attempt to dynamically load the contrib
		// instrumentation, when available, because we no longer take this dependency for .NET 9 targets.
		new()
		{
			Name = "HTTP",
			AssemblyName = "OpenTelemetry.Instrumentation.Http",
			FullyQualifiedType = "OpenTelemetry.Trace.HttpClientInstrumentationTracerProviderBuilderExtensions",
			InstrumentationMethod = "AddHttpClientInstrumentation"
		},

		new()
		{
			Name = "Kafka (Producer)",
			AssemblyName = "OpenTelemetry.Instrumentation.ConfluentKafka",
			FullyQualifiedType = "OpenTelemetry.Trace.TracerProviderBuilderExtensions",
			InstrumentationMethod = "AddKafkaProducerInstrumentation"
		},

		new()
		{
			Name = "Kafka (Consumer)",
			AssemblyName = "OpenTelemetry.Instrumentation.ConfluentKafka",
			FullyQualifiedType = "OpenTelemetry.Trace.TracerProviderBuilderExtensions",
			InstrumentationMethod = "AddKafkaConsumerInstrumentation"
		},

		new()
		{
			Name = "Owin",
			AssemblyName = "OpenTelemetry.Instrumentation.Owin",
			FullyQualifiedType = "OpenTelemetry.Trace.TracerProviderBuilderExtensions",
			InstrumentationMethod = "AddOwinInstrumentation"
		},

		new()
		{
			Name = "Quartz",
			AssemblyName = "OpenTelemetry.Instrumentation.Quartz",
			FullyQualifiedType = "OpenTelemetry.Trace.TracerProviderBuilderExtensions",
			InstrumentationMethod = "AddQuartzInstrumentation"
		},

		new()
		{
			Name = "ServiceFabricRemoting",
			AssemblyName = "OpenTelemetry.Instrumentation.ServiceFabricRemoting",
			FullyQualifiedType = "OpenTelemetry.Trace.TracerProviderBuilderExtensions",
			InstrumentationMethod = "AddServiceFabricRemotingInstrumentation"
		},

		new()
		{
			Name = "SqlClient",
			AssemblyName = "OpenTelemetry.Instrumentation.SqlClient",
			FullyQualifiedType = "OpenTelemetry.Trace.TracerProviderBuilderExtensions",
			InstrumentationMethod = "AddSqlClientInstrumentation"
		},

		new()
		{
			Name = "StackExchangeRedis",
			AssemblyName = "OpenTelemetry.Instrumentation.StackExchangeRedis",
			FullyQualifiedType = "OpenTelemetry.Trace.TracerProviderBuilderExtensions",
			InstrumentationMethod = "AddRedisInstrumentation"
		},

		new()
		{
			Name = "WCF",
			AssemblyName = "OpenTelemetry.Instrumentation.Wcf",
			FullyQualifiedType = "OpenTelemetry.Trace.TracerProviderBuilderExtensions",
			InstrumentationMethod = "AddWcfInstrumentation"
		},
	];
}
