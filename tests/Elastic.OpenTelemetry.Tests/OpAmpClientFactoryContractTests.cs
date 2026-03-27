// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Core.Configuration;
using Elastic.OpenTelemetry.OpAmp;
using Elastic.OpenTelemetry.OpAmp.Abstractions;

namespace Elastic.OpenTelemetry.Tests;

/// <summary>
/// Verifies the reflection contract between <see cref="OpAmpClientContract"/> and
/// <see cref="ElasticOpAmpClientFactory"/>. These tests are a secondary safety net
/// for things the compiler cannot check: type name strings and parameterless
/// constructor presence.
/// </summary>
public class OpAmpClientFactoryContractTests
{
	[Fact]
	public void FactoryTypeName_MatchesContract() =>
		Assert.Equal(OpAmpClientContract.FactoryTypeName, typeof(ElasticOpAmpClientFactory).FullName);

	[Fact]
	public void Factory_HasParameterlessConstructor() =>
		Assert.NotNull(typeof(ElasticOpAmpClientFactory).GetConstructor(Type.EmptyTypes));

	[Fact]
	public void Factory_ImplementsIOpAmpClientFactory() =>
		Assert.True(typeof(IOpAmpClientFactory).IsAssignableFrom(typeof(ElasticOpAmpClientFactory)));

	[Fact]
	public void FactoryCreateMethod_ParameterCount()
	{
		var method = typeof(IOpAmpClientFactory).GetMethod(nameof(IOpAmpClientFactory.Create));
		Assert.NotNull(method);
		Assert.Equal(6, method!.GetParameters().Length);
	}
}
