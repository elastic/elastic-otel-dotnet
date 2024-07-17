// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using NetEscapades.EnumGenerators;

namespace Elastic.OpenTelemetry.Configuration.Instrumentations;

/// <summary> A hash set to enable <see cref="TraceInstrumentation"/></summary>
public class TraceInstrumentations : HashSet<TraceInstrumentation>
{
	/// <summary> All available <see cref="TraceInstrumentation"/> </summary>
	public static readonly TraceInstrumentations All = new([.. TraceInstrumentationExtensions.GetValues()]);

	/// <summary> Explicitly enable specific <see cref="TraceInstrumentation"/> </summary>
	public TraceInstrumentations(IEnumerable<TraceInstrumentation> instrumentations) : base(instrumentations) { }

	/// <inheritdoc cref="object.ToString"/>
	public override string ToString()
	{
		if (Count == 0)
			return "None";
		if (Count == All.Count)
			return "All";
		if (All.Count - Count < 5)
			return $"All Except: {string.Join(", ", All.Except(this).Select(i => i.ToStringFast()))}";
		return string.Join(", ", this.Select(i => i.ToStringFast()));
	}
}

/// <summary> Available trace instrumentations. </summary>
[EnumExtensions]
public enum TraceInstrumentation
{
	///<summary>ASP.NET (.NET Framework) MVC / WebApi </summary>
	AspNet,

	///<summary>ASP.NET Core</summary>
	AspNetCore,

	///<summary>Azure SDK</summary>
	Azure,

	///<summary>Elastic.Clients.Elasticsearch</summary>
	Elasticsearch,

	///<summary>Elastic.Transport &gt;=0.4.16</summary>
	ElasticTransport,

	///<summary>Microsoft.EntityFrameworkCore Not supported on.NET Framework &gt;=6.0.12 </summary>
	EntityFrameworkCore,

	///<summary>GraphQL Not supported on.NET Framework &gt;=7.5.0 </summary>
	Graphql,

	///<summary>Grpc.Net.Client &gt;=2.52 .0 &amp; &lt; 3.0.0 </summary>
	GrpcNetClient,

	///<summary>System.Net.Http.HttpClient and System.Net.HttpWebRequest </summary>
	HttpClient,

	///<summary>Confluent.Kafka &gt;=1.4 .0 &amp; &lt; 3.0.0</summary>
	Kafka,

	///<summary>MassTransit Not supported on.NET Framework ≥8.0.0 </summary>
	MassTransit,

	///<summary>MongoDB.Driver.Core &gt;=2.13 .3 &amp; &lt; 3.0.0 </summary>
	MongoDb,

	///<summary>MySqlConnector &gt;=2.0.0 </summary>
	MysqlConnector,

	///<summary>MySql.Data Not supported on.NET Framework &gt;=8.1.0 </summary>
	MysqlData,

	///<summary>Npgsql &gt;=6.0.0</summary>
	Npgsql,

	///<summary>NServiceBus &gt;=8.0.0 &amp; &lt; 10.0.0 </summary>
	NServiceBus,

	///<summary>Oracle.ManagedDataAccess.Core and Oracle.ManagedDataAccess Not supported on ARM64 &gt;=23.4.0 </summary>
	OracleMda,

	///<summary>Quartz Not supported on.NET Framework 4.7.1 and older &gt;=3.4.0 </summary>
	Quartz,

	///<summary>Microsoft.Data.SqlClient, System.Data.SqlClient and System.Data (shipped with.NET Framework)</summary>
	SqlClient,

	///<summary>StackExchange.Redis Not supported on.NET Framework &gt;=2.0.405 &amp; &lt; 3.0.0 </summary>
	StackExchangeRedis,

	///<summary>WCF</summary>
	WcfClient,

	///<summary>WCF Not supported on.NET.</summary>
	WcfService
}
