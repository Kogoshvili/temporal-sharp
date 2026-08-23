using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Kogoshvili.Temporal.Analyzers.Analysis;
using Kogoshvili.Temporal.Analyzers.Diagnostics;

namespace Kogoshvili.Temporal.Analyzers.Analyzers;

/// <summary>
/// Flags error-handling mistakes in workflow and activity code: throwing a base
/// <c>Exception</c> instead of an <c>ApplicationFailure</c> (TMP2132), and
/// <c>Debug.Assert</c> / <c>Trace.Assert</c> in production workflow code (TMP2133).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ErrorHandlingAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> AssertMembers = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "System.Diagnostics.Debug.Assert",
        "System.Diagnostics.Trace.Assert");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.ThrowsBaseException,
            DiagnosticDescriptors.AssertInWorkflow);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
        {
            var state = CompilationAnalysisState.Get(startContext.Compilation, startContext.Options);

            startContext.RegisterSyntaxNodeAction(
                c => AnalyzeThrow(c, state),
                SyntaxKind.ThrowStatement);

            startContext.RegisterSyntaxNodeAction(
                c => AnalyzeAssert(c, state),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void AnalyzeThrow(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var throwStatement = (ThrowStatementSyntax)context.Node;
        if (throwStatement.Expression is not ObjectCreationExpressionSyntax creation)
        {
            return;
        }

        var type = context.SemanticModel.GetTypeInfo(creation).Type;
        if (type is null)
        {
            return;
        }

        // Only ApplicationFailureException fails a workflow; every other
        // exception type retries the workflow task indefinitely.
        if (TypeNames.IsOrDerivesFrom(type, "Temporalio.Exceptions.ApplicationFailureException"))
        {
            return;
        }

        if (state.IsWorkflowReachable(throwStatement, context.SemanticModel))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ThrowsBaseException,
                throwStatement.ThrowKeyword.GetLocation()));
            return;
        }

        // Activities may throw, but throwing a bare base exception is still a
        // smell (prefer ApplicationFailureException for a typed failure).
        if (GetEnclosingActivityMethod(context, throwStatement) is not null &&
            TypeNames.FullName(type) is "System.Exception" or "System.SystemException")
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ThrowsBaseException,
                throwStatement.ThrowKeyword.GetLocation()));
        }
    }

    private static void AnalyzeAssert(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method ||
            !AssertMembers.Contains(SymbolKeys.Member(method)))
        {
            return;
        }

        if (!state.IsWorkflowReachable(invocation, context.SemanticModel))
        {
            return;
        }

        var display = method.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.AssertInWorkflow,
            invocation.GetLocation(),
            display));
    }

    private static IMethodSymbol? GetEnclosingActivityMethod(SyntaxNodeAnalysisContext context, SyntaxNode node)
    {
        var enclosing = context.SemanticModel.GetEnclosingSymbol(node.SpanStart);
        for (var current = enclosing; current is not null; current = current.ContainingSymbol)
        {
            if (current is IMethodSymbol { MethodKind: not (MethodKind.LambdaMethod or MethodKind.LocalFunction) } method)
            {
                return WorkflowDetection.IsActivityMethod(method) ? method : null;
            }
        }

        return null;
    }
}
