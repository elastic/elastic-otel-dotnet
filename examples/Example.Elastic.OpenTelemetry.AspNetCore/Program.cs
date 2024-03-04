// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Example.Elastic.OpenTelemetry.AspNetCore.Controllers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services
	.AddHttpClient()
	.AddElasticOpenTelemetryForAspNetCore(HomeController.ActivitySourceName);

builder.Services
	.AddControllersWithViews();

var app = builder.Build();

app.Logger.LogInformation("Process Id {ProcesId}", Environment.ProcessId);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Home/Error");
	// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
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
