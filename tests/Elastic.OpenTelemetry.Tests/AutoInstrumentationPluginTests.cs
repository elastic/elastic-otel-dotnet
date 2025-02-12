// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Core;

namespace Elastic.OpenTelemetry.Tests;

public class AutoInstrumentationPluginTests
{
	[Fact]
	public void WritesErrorWhenUnableToBootstrap()
	{
		var sut = new TestableAutoInstrumentationPlugin();

		var error = sut.GetErrorText();

		error.Should().StartWith("Unable to bootstrap EDOT .NET due to");
		error.Should().Contain(TestableAutoInstrumentationPlugin.ExceptionMessage);
	}

	private class TestableAutoInstrumentationPlugin : AutoInstrumentationPlugin
	{
		public const string ExceptionMessage = "This is a test exception!!";

		private readonly StringWriter _textWriter = new();

		internal override void SetError() => Console.SetError(_textWriter);

		internal override BootstrapInfo GetBootstrapInfo(out ElasticOpenTelemetryComponents? components)
		{
			components = null;
			return new(SdkActivationMethod.NuGet, new Exception(ExceptionMessage));
		}

		public string GetErrorText() => _textWriter.ToString(); 
	}
}
