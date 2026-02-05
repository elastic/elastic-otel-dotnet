// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Threading.Tasks;
using Elastic.OpenTelemetry.Configuration.Analyzer.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Elastic.OpenTelemetry.Analyzer.Tests;

public class NestedWithElasticAnalyzerTests
{
	[Fact]
	public async Task WhenWithElasticDefaultsIsNestedInElastic_Should_RaiseWarning()
	{
		var testCode = @"
using System;

public class Builder
{
    public ServiceCollection Services { get; } = new ServiceCollection();
}

public class ServiceCollection
{
    public ServiceCollection AddOpenTelemetry() => this;
    
    public ServiceCollection WithElasticDefaults() => this;
    public ServiceCollection WithElasticTracing(object arg1, Action<ServiceCollection> arg2)
    {
        arg2(this);
        return this;
    }
}

class Program
{
    static void Main()
    {
        var builder = new Builder();
		builder.Services.AddOpenTelemetry()
			.WithElasticTracing(null, t => t.WithElasticDefaults());
    }
}
";

		var expected = new DiagnosticResult(NestedWithElasticAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
			.WithSpan(27, 37, 27, 56)
			.WithArguments("WithElasticDefaults");

		var test = new CSharpAnalyzerTest<NestedWithElasticAnalyzer, DefaultVerifier>
		{
			TestCode = testCode,
			ExpectedDiagnostics = { expected }
		};

		await test.RunAsync();
	}

	[Fact]
	public async Task WhenWithElasticDefaultsIsNotNestedInElastic_Should_NotRaiseWarning()
	{
		var testCode = @"
using System;

public class Builder
{
    public ServiceCollection Services { get; } = new ServiceCollection();
}

public class ServiceCollection
{
    public ServiceCollection AddOpenTelemetry() => this;
    
    public ServiceCollection WithElasticDefaults() => this;
    public ServiceCollection WithElasticTracing(object arg1, Action<ServiceCollection> arg2)
    {
        arg2(this);
        return this;
    }
}

class Program
{
    static void Main()
    {
        var builder = new Builder();
		builder.Services.AddOpenTelemetry()
			.WithElasticTracing(null, null);
    }
}
";
		var test = new CSharpAnalyzerTest<NestedWithElasticAnalyzer, DefaultVerifier>
		{
			TestCode = testCode,
			// No ExpectedDiagnostics added!
		};

		await test.RunAsync();
	}
}
