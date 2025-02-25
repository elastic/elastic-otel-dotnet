// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/", () => { });

app.MapGet("/http", static async ctx =>
{
	var client = new HttpClient();
	using var response = await client.GetAsync("https://example.com");
	ctx.Response.StatusCode = 200;
});

app.Run();

public partial class Program { }
