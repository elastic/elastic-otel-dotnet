// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Nest;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(new ElasticClient(new ConnectionSettings(new Uri("https://es-playground-ad2d97.es.eu-west-1.aws.elastic.cloud"))
	.BasicAuthentication("elastic", "EY8ZpgaQ4vJjKyaGsuSchPSP")
	.EnableApiVersioningHeader()
	.EnableDebugMode()));

var app = builder.Build();

app.MapGet("/", () => {});

app.MapGet("/nest", static async ctx =>
{
	var client = ctx.RequestServices.GetRequiredService<ElasticClient>();

	var response = await client.PingAsync();

	await ctx.Response.WriteAsync("NEST invoked");
});

app.Run();

public partial class Program { }
