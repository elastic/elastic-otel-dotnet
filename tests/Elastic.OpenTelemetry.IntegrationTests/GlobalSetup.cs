// Licensed to Elasticsearch B.V under
// one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

global using Xunit;
global using System.Diagnostics;
global using FluentAssertions;
using System.Runtime.CompilerServices;
using Xunit.Extensions.AssemblyFixture;

[assembly: TestFramework(AssemblyFixtureFramework.TypeName, AssemblyFixtureFramework.AssemblyName)]

namespace Elastic.OpenTelemetry.IntegrationTests;

public static class GlobalSetup
{
	[ModuleInitializer]
	public static void Setup() =>
		XunitContext.EnableExceptionCapture();
}
