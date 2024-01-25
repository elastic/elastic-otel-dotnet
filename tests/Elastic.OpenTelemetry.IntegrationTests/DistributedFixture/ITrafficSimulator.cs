// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Net;

namespace Elastic.OpenTelemetry.IntegrationTests.DistributedFixture;

public interface ITrafficSimulator
{
	Task Start(DistributedApplicationFixture distributedInfra);
}

public class DefaultTrafficSimulator : ITrafficSimulator
{
	public async Task Start(DistributedApplicationFixture distributedInfra)
	{
		for (var i = 0; i < 10; i++)
		{
			var get = await distributedInfra.AspNetApplication.HttpClient.GetAsync("e2e");
			get.StatusCode.Should().Be(HttpStatusCode.OK);
			var response = await get.Content.ReadAsStringAsync();
		}
	}
}
