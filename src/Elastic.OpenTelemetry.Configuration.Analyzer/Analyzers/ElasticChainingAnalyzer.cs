// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Elastic.OpenTelemetry.Configuration.Analyzer;

/// <summary>
/// Roslyn analyzer that raises a warning if a method starting with 'WithElastic' is called
/// after or inside a method named 'AddElasticOpenTelemetry' or 'AddOpenTelemetry'.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ElasticChainingAnalyzer : DiagnosticAnalyzer
{
	/// <summary>
	/// The unique diagnostic ID for this analyzer.
	/// </summary>
	public const string DiagnosticId = "EDOT001";
	private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
		DiagnosticId,
		"WithElastic called after AddElasticOpenTelemetry or AddOpenTelemetry",
        "Avoid calling '{0}' after or inside 'AddElasticOpenTelemetry'. It is discouraged.",
		"Usage",
		DiagnosticSeverity.Warning,
		isEnabledByDefault: true);

	/// <summary>
	/// The diagnostic descriptor that defines the rule, including ID, title, message, category, and severity.
	/// </summary>
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

	/// <summary>
	/// Initializes the analyzer and registers the syntax node action.
	/// </summary>
	/// <param name="context">The analysis context.</param>
	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
	}

	private void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
	{
		var invocation = (InvocationExpressionSyntax)context.Node;

		// Get the method name
		if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
		{
			var methodName = memberAccess.Name.Identifier.Text;

			// Check for WithElastic* methods
			if (methodName.StartsWith("WithElastic"))
			{
				// Walk up the chain to see if AddElasticOpenTelemetry or AddOpenTelemetry is present
				var expr = memberAccess.Expression;
				while (expr is InvocationExpressionSyntax parentInvocation)
				{
					if (parentInvocation.Expression is MemberAccessExpressionSyntax parentMemberAccess)
					{
						var parentMethodName = parentMemberAccess.Name.Identifier.Text;
						if (parentMethodName == "AddElasticOpenTelemetry")
						{
							// Found the pattern, report diagnostic
							var diagnostic = Diagnostic.Create(
								Rule,
								memberAccess.Name.GetLocation(),
								methodName);
							context.ReportDiagnostic(diagnostic);
							break;
						}
						expr = parentMemberAccess.Expression;
					}
				}
			}
		}
	}
}
