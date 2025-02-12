// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Configuration.Instrumentations;

namespace Elastic.OpenTelemetry.Tests.Configuration.Instrumentations;

public class LogInstrumentationsTests
{
	[Fact]
	public void AllTest()
	{
		var instrumentations = new LogInstrumentations([LogInstrumentation.ILogger]);

		Assert.Equal("All", instrumentations.ToString());
	}

	[Fact]
	public void NoneTest()
	{
		var instrumentations = new LogInstrumentations([]);

		Assert.Equal("None", instrumentations.ToString());
	}
}
