// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Elastic.OpenTelemetry.Configuration.Analyzer.Analyzers;

/// <summary>
/// Roslyn analyzer that raises a warning if a method starting with 'WithElastic'
/// is called inside another method starting with 'WithElastic'.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NestedWithElasticAnalyzer : DiagnosticAnalyzer
{
	/// <summary>
	/// The unique diagnostic ID for this analyzer.
	/// </summary>
	public const string DiagnosticId = "EDOT002";
	private static readonly DiagnosticDescriptor Rule = new(
		DiagnosticId,
		"Nested WithElastic method call",
		"Avoid calling '{0}' inside another 'WithElastic*' method. It is discouraged.",
		"Usage",
		DiagnosticSeverity.Warning,
		isEnabledByDefault: true);

	/// <summary>
	/// Gets the set of supported diagnostics for this analyzer.
	/// </summary>
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

	/// <summary>
	/// Initializes the analyzer and registers the syntax node action for invocation expressions.
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

		// Check if this is a WithElastic* method
		if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
		{
			var methodName = memberAccess.Name.Identifier.Text;
			if (methodName.StartsWith("WithElastic"))
			{
				// Walk up the syntax tree to see if this invocation is inside another WithElastic* invocation
				var parent = invocation.Parent;
				while (parent != null)
				{
					if (parent is ArgumentSyntax argParent)
					{
						// Check if the argument is part of a WithElastic* invocation
						var invocationParent = argParent.Parent?.Parent as InvocationExpressionSyntax;
						if (invocationParent?.Expression is MemberAccessExpressionSyntax parentMemberAccess)
						{
							var parentMethodName = parentMemberAccess.Name.Identifier.Text;
							if (parentMethodName.StartsWith("WithElastic"))
							{
								// Found a WithElastic* inside another WithElastic*
								var diagnostic = Diagnostic.Create(
									Rule,
									memberAccess.Name.GetLocation(),
									methodName);
								context.ReportDiagnostic(diagnostic);
								return;
							}
						}
					}
					parent = parent.Parent;
				}
			}
		}
	}
}
