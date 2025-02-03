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
