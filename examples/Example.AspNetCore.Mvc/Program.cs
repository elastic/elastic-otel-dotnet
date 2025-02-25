// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using OpenTelemetry;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services
	.AddHttpClient()
	.AddElasticOpenTelemetry()
		.WithTracing(t => t.AddAspNetCoreInstrumentation()); // This is redundant but used in manual testing for now

builder.Services
	.AddControllersWithViews();

var app = builder.Build();

app.Logger.LogInformation("Process Id {ProcessId}", Environment.ProcessId);

if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Home/Error");
	app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
	"default",
	"{controller=Home}/{action=Index}/{id?}");

app.Run();
