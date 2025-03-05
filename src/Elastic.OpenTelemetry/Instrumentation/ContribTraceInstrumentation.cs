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
			Filename = "OpenTelemetry.Instrumentation.AspNet.dll",
			FullyQualifiedType = "OpenTelemetry.Trace.TracerProviderBuilderExtensions",
			InstrumentationMethod = "AddAspNetInstrumentation"
		},

		new()
		{
			Name = "AspNetCore",
			Filename = "OpenTelemetry.Instrumentation.AspNetCore.dll",
			FullyQualifiedType = "OpenTelemetry.Trace.AspNetCoreInstrumentationTracerProviderBuilderExtensions",
			InstrumentationMethod = "AddAspNetCoreInstrumentation"
		},

		new()
		{
			Name = "AWS",
			Filename = "OpenTelemetry.Instrumentation.AWS.dll",
			FullyQualifiedType = "OpenTelemetry.Trace.TracerProviderBuilderExtensions",
			InstrumentationMethod = "AddAWSInstrumentation"
		},

		new()
		{
			Name = "Kafka (Producer)",
			Filename = "OpenTelemetry.Instrumentation.ConfluentKafka.dll",
			FullyQualifiedType = "OpenTelemetry.Trace.TracerProviderBuilderExtensions",
			InstrumentationMethod = "AddKafkaProducerInstrumentation"
		},

		new()
		{
			Name = "Kafka (Consumer)",
			Filename = "OpenTelemetry.Instrumentation.ConfluentKafka.dll",
			FullyQualifiedType = "OpenTelemetry.Trace.TracerProviderBuilderExtensions",
			InstrumentationMethod = "AddKafkaConsumerInstrumentation"
		},

		new()
		{
			Name = "EntityFrameworkCore",
			Filename = "OpenTelemetry.Instrumentation.EntityFrameworkCore.dll",
			FullyQualifiedType = "OpenTelemetry.Trace.TracerProviderBuilderExtensions",
			InstrumentationMethod = "AddEntityFrameworkCoreInstrumentation"
		},

		new()
		{
			Name = "GrpcCore",
			Filename = "OpenTelemetry.Instrumentation.GrpcCore.dll",
			FullyQualifiedType = "OpenTelemetry.Trace.TracerProviderBuilderExtensions",
			InstrumentationMethod = "AddGrpcCoreInstrumentation"
		},

		new()
		{
			Name = "Hangfire",
			Filename = "OpenTelemetry.Instrumentation.Hangfire.dll",
			FullyQualifiedType = "OpenTelemetry.Trace.TracerProviderBuilderExtensions",
			InstrumentationMethod = "AddHangfireInstrumentation"
		},

#if NET9_0_OR_GREATER
		// On .NET 9, we add the `System.Net.Http` source for native instrumentation, rather than referencing
		// the contrib instrumentation. However, if the consuming application has their own reference to
		// `OpenTelemetry.Instrumentation.Http`, then we use that since it signals the consumer prefers the
		// contrib instrumentation. Therefore, on .NET 9+ targets, we attempt to dynamically load the contrib
		// instrumentation, when available, because we no longer take this dependency for .NET 9 targets.
		new()
		{
			Name = "HTTP",
			Filename = "OpenTelemetry.Instrumentation.Http.dll",
			FullyQualifiedType = "OpenTelemetry.Trace.HttpClientInstrumentationTracerProviderBuilderExtensions",
			InstrumentationMethod = "AddHttpClientInstrumentation"
		},
#endif

		new()
		{
			Name = "NEST",
			Filename = "OpenTelemetry.Instrumentation.ElasticsearchClient.dll",
			FullyQualifiedType = "OpenTelemetry.Trace.TracerProviderBuilderExtensions",
			InstrumentationMethod = "AddElasticsearchClientInstrumentation"
		},

		new()
		{
			Name = "Owin",
			Filename = "OpenTelemetry.Instrumentation.Owin.dll",
			FullyQualifiedType = "OpenTelemetry.Trace.TracerProviderBuilderExtensions",
			InstrumentationMethod = "AddOwinInstrumentation"
		},

		new()
		{
			Name = "Quartz",
			Filename = "OpenTelemetry.Instrumentation.Quartz.dll",
			FullyQualifiedType = "OpenTelemetry.Trace.TracerProviderBuilderExtensions",
			InstrumentationMethod = "AddQuartzInstrumentation"
		},

		new()
		{
			Name = "ServiceFabricRemoting",
			Filename = "OpenTelemetry.Instrumentation.ServiceFabricRemoting.dll",
			FullyQualifiedType = "OpenTelemetry.Trace.TracerProviderBuilderExtensions",
			InstrumentationMethod = "AddServiceFabricRemotingInstrumentation"
		},

		new()
		{
			Name = "SqlClient",
			Filename = "OpenTelemetry.Instrumentation.SqlClient.dll",
			FullyQualifiedType = "OpenTelemetry.Trace.TracerProviderBuilderExtensions",
			InstrumentationMethod = "AddSqlClientInstrumentation"
		},

		new()
		{
			Name = "StackExchangeRedis",
			Filename = "OpenTelemetry.Instrumentation.StackExchangeRedis.dll",
			FullyQualifiedType = "OpenTelemetry.Trace.TracerProviderBuilderExtensions",
			InstrumentationMethod = "AddRedisInstrumentation"
		},

		new()
		{
			Name = "WCF",
			Filename = "OpenTelemetry.Instrumentation.Wcf.dll",
			FullyQualifiedType = "OpenTelemetry.Trace.TracerProviderBuilderExtensions",
			InstrumentationMethod = "AddWcfInstrumentation"
		},
	];
}
