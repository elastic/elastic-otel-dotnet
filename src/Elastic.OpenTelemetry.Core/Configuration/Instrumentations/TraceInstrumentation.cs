// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using NetEscapades.EnumGenerators;

namespace Elastic.OpenTelemetry.Configuration.Instrumentations;

/// <summary>
/// A hash set to enable <see cref="TraceInstrumentation"/> for auto-instrumentation.
/// </summary>
/// <remarks>
/// Explicitly enable specific <see cref="TraceInstrumentation"/> libraries.
/// </remarks>
internal class TraceInstrumentations(IEnumerable<TraceInstrumentation> instrumentations) : HashSet<TraceInstrumentation>(instrumentations)
{
	/// <summary>
	/// All available <see cref="TraceInstrumentation"/> libraries.
	/// </summary>
	public static readonly TraceInstrumentations All = new([.. TraceInstrumentationExtensions.GetValues()]);

	/// <inheritdoc cref="object.ToString"/>
	public override string ToString()
	{
		if (Count == 0)
			return "None";
		if (Count == All.Count)
			return "All";
		if (All.Count - Count < All.Count)
			return $"All Except: {string.Join(", ", All.Except(this).Select(i => i.ToStringFast()))}";

		return string.Join(", ", this.Select(i => i.ToStringFast()));
	}
}

/// <summary>
/// Available trace instrumentations.
/// </summary>
[EnumExtensions]
internal enum TraceInstrumentation
{
	/// <summary>ASP.NET (.NET Framework) MVC / WebApi.</summary>
	AspNet,

	/// <summary>ASP.NET Core.</summary>
	AspNetCore,

	/// <summary>Azure SDK.</summary>
	Azure,

	/// <summary>Elastic.Clients.Elasticsearch.</summary>
	Elasticsearch,

	/// <summary>Elastic.Transport.</summary>
	ElasticTransport,

	/// <summary>Microsoft.EntityFrameworkCore.</summary>
	EntityFrameworkCore,

	/// <summary>GraphQL.</summary>
	Graphql,

	/// <summary>Grpc.Net.Client.</summary>
	GrpcNetClient,

	/// <summary>System.Net.Http.HttpClient and System.Net.HttpWebRequest.</summary>
	HttpClient,

	/// <summary>Confluent.Kafka.</summary>
	Kafka,

	/// <summary>MassTransit.</summary>
	MassTransit,

	/// <summary>MongoDB.Driver.</summary>
	MongoDb,

	/// <summary>MySqlConnector.</summary>
	MysqlConnector,

	/// <summary>MySql.Data.</summary>
	MysqlData,

	/// <summary>Npgsql &gt;=6.0.0.</summary>
	Npgsql,

	/// <summary>NServiceBus.</summary>
	NServiceBus,

	/// <summary>Oracle.ManagedDataAccess.Core and Oracle.ManagedDataAccess.</summary>
	OracleMda,

	/// <summary>Quartz.</summary>
	Quartz,

	/// <summary>Microsoft.Data.SqlClient, System.Data.SqlClient and System.Data (shipped with.NET Framework).</summary>
	SqlClient,

	/// <summary>StackExchange.Redis.</summary>
	StackExchangeRedis,

	/// <summary>WCF client.</summary>
	WcfClient,

	/// <summary>WCF server.</summary>
	WcfService
}
