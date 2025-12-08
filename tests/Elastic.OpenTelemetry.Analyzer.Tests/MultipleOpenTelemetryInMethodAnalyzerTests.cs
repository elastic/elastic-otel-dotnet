using System.Threading.Tasks;
using Elastic.OpenTelemetry.Configuration.Analyzer.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Elastic.OpenTelemetry.Analyzer.Tests;

public class MultipleOpenTelemetryInMethodAnalyzerTests
{
	[Fact]
	public async Task WhenAddOpenTelemetryCalledTwice_Should_RaiseWarning()
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
	public ServiceCollection AddElasticOpenTelemetry() => this;
}

class Program
{
    static void Main()
    {
        var builder = new Builder();
        builder.Services.AddOpenTelemetry();
        builder.Services.AddOpenTelemetry();
    }
}
";

		var expected1 = new DiagnosticResult(MultipleOpenTelemetryInMethodAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
			.WithSpan(20, 26, 20, 42)
			.WithArguments("AddOpenTelemetry");

		var expected2 = new DiagnosticResult(MultipleOpenTelemetryInMethodAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
			.WithSpan(21, 26, 21, 42)
			.WithArguments("AddOpenTelemetry");

		var test = new CSharpAnalyzerTest<MultipleOpenTelemetryInMethodAnalyzer, DefaultVerifier>
		{
			TestCode = testCode,
			ExpectedDiagnostics = { expected1, expected2 }
		};

		await test.RunAsync();
	}

	[Fact]
	public async Task WhenAddOpenTelemetryCalledAndAddElasticOpenTelemetryCalled_Should_RaiseWarning()
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
	public ServiceCollection AddElasticOpenTelemetry() => this;
}

class Program
{
    static void Main()
    {
        var builder = new Builder();
        builder.Services.AddOpenTelemetry();
        builder.Services.AddElasticOpenTelemetry();
    }
}
";

		var expected1 = new DiagnosticResult(MultipleOpenTelemetryInMethodAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
			.WithSpan(20, 26, 20, 42)
			.WithArguments("AddOpenTelemetry");

		var expected2 = new DiagnosticResult(MultipleOpenTelemetryInMethodAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
			.WithSpan(21, 26, 21, 49)
			.WithArguments("AddElasticOpenTelemetry");

		var test = new CSharpAnalyzerTest<MultipleOpenTelemetryInMethodAnalyzer, DefaultVerifier>
		{
			TestCode = testCode,
			ExpectedDiagnostics = { expected1, expected2 }
		};

		await test.RunAsync();
	}
}
