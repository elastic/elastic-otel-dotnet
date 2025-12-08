using System.Threading.Tasks;
using Elastic.OpenTelemetry.Configuration.Analyzer.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Elastic.OpenTelemetry.Analyzer.Tests;

public class ElasticChainingAnalyzerTests
{
	[Fact]
	public async Task WhenWithElasticDefaultsIsChainedWithAddElasticOpenTelemetry_Should_RaiseWarning()
	{
		var testCode = @"
using System;

public class Builder
{
    public ServiceCollection Services { get; } = new ServiceCollection();
}

public class ServiceCollection
{
    public ServiceCollection AddElasticOpenTelemetry() => this;
    public ServiceCollection WithTracing() => this;
    public ServiceCollection WithElasticDefaults() => this;
}

class Program
{
    static void Main()
    {
        var builder = new Builder();
        builder.Services.AddElasticOpenTelemetry()
            .WithTracing()
            .WithElasticDefaults();
    }
}
";

		var expected = new DiagnosticResult(ElasticChainingAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
			.WithSpan(23, 14, 23, 33)
			.WithArguments("WithElasticDefaults");

		var test = new CSharpAnalyzerTest<ElasticChainingAnalyzer, DefaultVerifier>
		{
			TestCode = testCode,
			ExpectedDiagnostics = { expected }
		};

		await test.RunAsync();
	}

	[Fact]
	public async Task WhenWithElasticDefaultsIsNotChainedWithAddElasticOpenTelemetry_Should_NotRaiseWarning()
	{
		var testCode = @"
using System;

public class Builder
{
    public ServiceCollection Services { get; } = new ServiceCollection();
}

public class ServiceCollection
{
    public ServiceCollection AddElasticOpenTelemetry() => this;
    public ServiceCollection WithTracing() => this;
    public ServiceCollection WithElasticDefaults() => this;
}

class Program
{
    static void Main()
    {
        var builder = new Builder();
        builder.Services.AddElasticOpenTelemetry()
            .WithTracing();
    }
}
";
		var test = new CSharpAnalyzerTest<ElasticChainingAnalyzer, DefaultVerifier>
		{
			TestCode = testCode,
			// No ExpectedDiagnostics added!
		};

		await test.RunAsync();
	}
}
