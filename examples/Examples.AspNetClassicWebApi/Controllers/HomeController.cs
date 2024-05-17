// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections.Generic;
using System.Diagnostics;
using System.Web.Http;

namespace Examples.AspNetClassicWebApi.Controllers;

public class HomeController : ApiController
{
	public IEnumerable<string> Get()
	{
		using var activity = WebApiApplication.ActivitySource.StartActivity("DoingStuff", ActivityKind.Internal);

		activity?.SetTag("custom-tag", "TagValue");

		return ["value1", "value2"];
	}
}
