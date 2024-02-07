// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information
using Microsoft.Extensions.DependencyInjection;

namespace Elastic.OpenTelemetry.Tests;

public class ProcessorTests
{
	[Fact]
	public void AllElasticProcessors_Should_BeAddedToDependencyInjectionContainer()
	{
		var sc = new ServiceCollection();
		sc.AddElasticOpenTelemetry();
		var sp = sc.BuildServiceProvider();

		var processors = AppDomain.CurrentDomain.GetAssemblies()
			.SelectMany(s => s.GetTypes())
			.Where(t => typeof(IElasticProcessor).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract)
			.ToArray();

		var registeredProcessors = sp.GetRequiredService<IEnumerable<IElasticProcessor>>().ToArray();

		processors.Length.Should().Be(registeredProcessors.Length);

		foreach (var processor in processors)
		{
			_ = registeredProcessors.Single(rp => rp.GetType() == processor);
		}
	}
}
