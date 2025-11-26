// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Elastic.OpenTelemetry.Configuration.Analyzer.Analyzers;

/// <summary>
/// Roslyn analyzer that raises a warning if AddOpenTelemetry or AddElasticOpenTelemetry
/// is called more than once in the same method or in top-level statements.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MultipleOpenTelemetryInMethodAnalyzer : DiagnosticAnalyzer
{
	/// <summary>
	/// The unique diagnostic ID for this analyzer.
	/// </summary>
	public const string DiagnosticId = "EDOT003";
	private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
		DiagnosticId,
		"Multiple AddOpenTelemetry/AddElasticOpenTelemetry calls in the same method or file",
		"Avoid calling the methods AddOpenTelemetry or AddElasticOpenTelemetry more than once in the same method or top-level statements. It is discouraged.",
		"Usage",
		DiagnosticSeverity.Warning,
		isEnabledByDefault: true);

	/// <summary>
	/// Gets the set of supported diagnostics for this analyzer.
	/// </summary>
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

	/// <summary>
	/// Initializes the analyzer and registers the syntax node actions for method declarations and compilation units.
	/// </summary>
	/// <param name="context">The analysis context.</param>
	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
		context.RegisterSyntaxNodeAction(AnalyzeCompilationUnit, SyntaxKind.CompilationUnit);
	}

	private void AnalyzeMethod(SyntaxNodeAnalysisContext context)
	{
		var methodDeclaration = (MethodDeclarationSyntax)context.Node;

		var invocations = methodDeclaration.DescendantNodes()
			.OfType<InvocationExpressionSyntax>()
			.Select(invocation => invocation.Expression as MemberAccessExpressionSyntax)
			.Where(memberAccess => memberAccess != null)
			.ToList();

		CheckAndReport(invocations, context);
	}

	private void AnalyzeCompilationUnit(SyntaxNodeAnalysisContext context)
	{
		var compilationUnit = (CompilationUnitSyntax)context.Node;

		// Only consider invocations that are direct children of the compilation unit (top-level statements)
		var invocations = compilationUnit.DescendantNodes()
			.OfType<InvocationExpressionSyntax>()
			.Where(invocation =>
			{
				// Find the first ancestor that is a method or local function
				var ancestor = invocation.Ancestors().FirstOrDefault(a =>
					a is MethodDeclarationSyntax ||
					a is LocalFunctionStatementSyntax ||
					a is AnonymousFunctionExpressionSyntax);
				// If there is no such ancestor, it's a top-level statement
				return ancestor == null;
			})
			.Select(invocation => invocation.Expression as MemberAccessExpressionSyntax)
			.Where(memberAccess => memberAccess != null)
			.ToList();

		CheckAndReport(invocations, context);
	}

	private void CheckAndReport(
	List<MemberAccessExpressionSyntax> invocations,
	SyntaxNodeAnalysisContext context)
	{
		var methodNames = new[] { "AddOpenTelemetry", "AddElasticOpenTelemetry" };
		var matches = invocations
			.Where(m => methodNames.Contains(m.Name.Identifier.Text))
			.ToList();

		if (matches.Count > 1)
		{
			foreach (var match in matches)
			{
				var diagnostic = Diagnostic.Create(
					Rule,
					match.Name.GetLocation(),
					match.Name.Identifier.Text);
				context.ReportDiagnostic(diagnostic);
			}
		}
	}
}
