// Licensed to Elasticsearch B.V under
// one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Net;
using Elastic.OpenTelemetry.IntegrationTests.DistributedFixture;
using Xunit.Extensions.AssemblyFixture;

namespace Elastic.OpenTelemetry.IntegrationTests;

public class EndToEndTests(DistributedApplicationFixture fixture)
	: IAssemblyFixture<DistributedApplicationFixture>
{
	[Fact]
	public async Task Test()
	{
		fixture.Started.Should().BeTrue();
		var http = new HttpClient();

		var get = await http.GetAsync("http://localhost:5247/e2e");

		get.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.InternalServerError);

		var response = await get.Content.ReadAsStringAsync();
	}
}
