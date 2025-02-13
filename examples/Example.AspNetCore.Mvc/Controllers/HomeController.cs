// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using System.Net;
using Example.AspNetCore.Mvc.Models;
using Microsoft.AspNetCore.Mvc;

namespace Example.AspNetCore.Mvc.Controllers;

public class HomeController(IHttpClientFactory httpClientFactory) : Controller
{
	public const string ActivitySourceName = "CustomActivitySource";
	private static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");

	private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

	public async Task<IActionResult> Index()
	{
		using var client = _httpClientFactory.CreateClient();

		// ReSharper disable once ExplicitCallerInfoArgument
		using var activity = ActivitySource.StartActivity("DoingStuff", ActivityKind.Internal);
		activity?.SetTag("CustomTag", "TagValue");

		await Task.Delay(100);
		var response = await client.GetAsync("http://elastic.co");
		await Task.Delay(50);

		activity?.SetStatus(response.StatusCode == HttpStatusCode.OK ? ActivityStatusCode.Ok : ActivityStatusCode.Error);

		return View();
	}

	public IActionResult Privacy() => View();

	[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
	public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
