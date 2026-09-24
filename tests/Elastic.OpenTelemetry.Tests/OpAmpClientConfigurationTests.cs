// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.OpAmp;
using OpenTelemetry.OpAmp.Client.Settings;

namespace Elastic.OpenTelemetry.Tests;

public class OpAmpClientConfigurationTests
{
	[Fact]
	public void GetConfigurationAction_EnablesRemoteConfiguration()
	{
		using var httpClient = new HttpClient();
		var settings = new OpAmpClientSettings();

		OpAmpClientConfiguration.GetConfigurationAction(
			"http://localhost:4320/v1/opamp",
			"test-service",
			null,
			httpClient)(settings);

		Assert.True(settings.RemoteConfiguration.AcceptsRemoteConfig);
	}
}
