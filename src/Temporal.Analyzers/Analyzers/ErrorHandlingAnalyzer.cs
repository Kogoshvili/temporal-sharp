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
            DiagnosticDescriptors.ActivityThrowsBaseException,
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

        // By default a workflow only fails on a FailureException or cancellation;
        // every other exception type retries the workflow task indefinitely. Users
        // may opt other types into failure via WorkflowFailureExceptionTypes, so
        // this is a Warning rather than an Error.
        if (TypeNames.IsOrDerivesFrom(type, "Temporalio.Exceptions.FailureException") ||
            TypeNames.IsOrDerivesFrom(type, "Temporalio.Exceptions.ApplicationFailureException") ||
            TypeNames.IsOrDerivesFrom(type, "System.OperationCanceledException"))
        {
            return;
        }

        // Update validators reject an update by throwing; a non-ApplicationFailure
        // exception there is the documented mechanism, not a workflow-failure path.
        if (GetEnclosingValidator(context, throwStatement) is not null)
        {
            return;
        }

        // A throw that is caught within the workflow is control flow, not a
        // workflow failure; only a throw that escapes the workflow retries the
        // workflow task forever.
        if (IsHandledByCatch(throwStatement))
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
                DiagnosticDescriptors.ActivityThrowsBaseException,
                throwStatement.ThrowKeyword.GetLocation()));
        }
    }

    private static bool IsHandledByCatch(ThrowStatementSyntax throwStatement)
    {
        for (var current = throwStatement.Parent; current is not null; current = current.Parent)
        {
            if (current is TryStatementSyntax { Catches.Count: > 0 } tryStatement &&
                tryStatement.Block.Contains(throwStatement))
            {
                return true;
            }
        }

        return false;
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

    private static IMethodSymbol? GetEnclosingValidator(SyntaxNodeAnalysisContext context, SyntaxNode node)
    {
        var enclosing = context.SemanticModel.GetEnclosingSymbol(node.SpanStart);
        for (var current = enclosing; current is not null; current = current.ContainingSymbol)
        {
            if (current is IMethodSymbol { MethodKind: not (MethodKind.LambdaMethod or MethodKind.LocalFunction) } method)
            {
                return WorkflowDetection.IsWorkflowUpdateValidatorMethod(method) ? method : null;
            }
        }

        return null;
    }
}
