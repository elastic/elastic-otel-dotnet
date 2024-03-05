// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace Example.Elastic.OpenTelemetry.AspNetCore.Controllers;

public class E2EController : Controller
{
	public async Task<IActionResult> Index()
	{
		var activityFeature = HttpContext.Features.Get<IHttpActivityFeature>();
		var activity = activityFeature?.Activity;
		activity?.AddBaggage("operation.success", true.ToString());
		activity?.SetTag("CustomTag", "TagValue");

		await Task.Delay(100);

		using var childActivity = activity?.Source.StartActivity(ActivityKind.Internal);
		await Task.Delay(200);

		return View();
	}

	public async Task<IActionResult> Fail()
	{
		var activityFeature = HttpContext.Features.Get<IHttpActivityFeature>();
		var activity = activityFeature?.Activity;
		activity?.AddBaggage("operation.success", false.ToString());
		activity?.SetTag("CustomTag", "TagValue");

		await Task.Delay(100);

		using var childActivity = activity?.Source.StartActivity(ActivityKind.Internal);
		await Task.Delay(200);

		throw new Exception("Random failure");
	}
}
