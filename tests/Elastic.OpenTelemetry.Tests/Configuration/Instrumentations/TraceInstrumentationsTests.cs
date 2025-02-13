// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Configuration.Instrumentations;

namespace Elastic.OpenTelemetry.Tests.Configuration.Instrumentations;

public class TraceInstrumentationsTests
{
	[Fact]
	public void AllTest()
	{
		var instrumentations = new TraceInstrumentations(
		[
			TraceInstrumentation.AspNet,
			TraceInstrumentation.AspNetCore,
			TraceInstrumentation.Azure,
			TraceInstrumentation.Elasticsearch,
			TraceInstrumentation.ElasticTransport,
			TraceInstrumentation.EntityFrameworkCore,
			TraceInstrumentation.Graphql,
			TraceInstrumentation.GrpcNetClient,
			TraceInstrumentation.HttpClient,
			TraceInstrumentation.Kafka,
			TraceInstrumentation.MassTransit,
			TraceInstrumentation.MongoDb,
			TraceInstrumentation.MysqlConnector,
			TraceInstrumentation.MysqlData,
			TraceInstrumentation.Npgsql,
			TraceInstrumentation.NServiceBus,
			TraceInstrumentation.OracleMda,
			TraceInstrumentation.Quartz,
			TraceInstrumentation.SqlClient,
			TraceInstrumentation.StackExchangeRedis,
			TraceInstrumentation.WcfClient,
			TraceInstrumentation.WcfService
		]);

		Assert.Equal("All", instrumentations.ToString());
	}

	[Fact]
	public void SomeTest()
	{
		var instrumentations = new TraceInstrumentations(
		[
			TraceInstrumentation.Azure,
			TraceInstrumentation.Elasticsearch,
			TraceInstrumentation.ElasticTransport,
			TraceInstrumentation.EntityFrameworkCore,
			TraceInstrumentation.Graphql,
			TraceInstrumentation.GrpcNetClient,
			TraceInstrumentation.HttpClient,
			TraceInstrumentation.Kafka,
			TraceInstrumentation.MassTransit,
			TraceInstrumentation.MongoDb,
			TraceInstrumentation.MysqlConnector,
			TraceInstrumentation.MysqlData,
			TraceInstrumentation.Npgsql,
			TraceInstrumentation.NServiceBus,
			TraceInstrumentation.OracleMda,
			TraceInstrumentation.Quartz,
			TraceInstrumentation.SqlClient,
			TraceInstrumentation.StackExchangeRedis,
			TraceInstrumentation.WcfClient,
			TraceInstrumentation.WcfService
		]);

		Assert.StartsWith("All Except: AspNet, AspNetCore", instrumentations.ToString());
	}

	[Fact]
	public void NoneTest()
	{
		var instrumentations = new TraceInstrumentations([]);

		Assert.Equal("None", instrumentations.ToString());
	}
}
