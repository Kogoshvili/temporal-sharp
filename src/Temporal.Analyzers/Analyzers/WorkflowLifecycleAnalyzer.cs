using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Kogoshvili.Temporal.Analyzers.Analysis;
using Kogoshvili.Temporal.Analyzers.Diagnostics;

namespace Kogoshvili.Temporal.Analyzers.Analyzers;

/// <summary>
/// Flags workflow lifecycle mistakes: continue-as-new without passing state
/// (TMP2122), swallowed cancellation (TMP2123), cancellable cleanup (TMP2124),
/// and unbounded loops that never continue-as-new (TMP2125).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WorkflowLifecycleAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> BroadCatchTypes = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "System.Exception",
        "System.SystemException",
        "System.OperationCanceledException",
        "System.Threading.Tasks.TaskCanceledException");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.ContinueAsNewWithoutState,
            DiagnosticDescriptors.SwallowedCancellation,
            DiagnosticDescriptors.CleanupNotNonCancellable,
            DiagnosticDescriptors.LongRunningLoopWithoutContinueAsNew);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
        {
            var state = CompilationAnalysisState.Get(startContext.Compilation, startContext.Options);

            startContext.RegisterSyntaxNodeAction(
                c => AnalyzeContinueAsNew(c, state),
                SyntaxKind.InvocationExpression);

            startContext.RegisterSyntaxNodeAction(
                c => AnalyzeCatch(c, state),
                SyntaxKind.CatchClause);

            startContext.RegisterSyntaxNodeAction(
                c => AnalyzeFinally(c, state),
                SyntaxKind.FinallyClause);

            startContext.RegisterSyntaxNodeAction(
                c => AnalyzeLoop(c, state),
                SyntaxKind.WhileStatement,
                SyntaxKind.ForStatement);
        });
    }

    // TMP2122 — continue-as-new that does not pass workflow state.
    private static void AnalyzeContinueAsNew(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method ||
            !IsWorkflowApi(method, "CreateContinueAsNewException"))
        {
            return;
        }

        if (!state.IsWorkflowReachable(invocation, context.SemanticModel))
        {
            return;
        }

        var target = LambdaTargetResolver.ResolveTypedLambdaTarget(context, invocation);
        if (HasNoStateArgument(invocation, target))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ContinueAsNewWithoutState,
                invocation.GetLocation()));
        }
    }

    private static bool HasNoStateArgument(InvocationExpressionSyntax invocation, IMethodSymbol? target)
    {
        if (target is not null && target.Parameters.Length == 0)
        {
            return true;
        }

        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            var expression = argument.Expression;

            if (IsNullLiteral(expression))
            {
                return true;
            }

            if (IsEmptyCollection(expression))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNullLiteral(ExpressionSyntax expression)
    {
        var current = expression;
        while (current is ParenthesizedExpressionSyntax parens)
        {
            current = parens.Expression;
        }

        return current is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression };
    }

    private static bool IsEmptyCollection(ExpressionSyntax expression)
    {
        var current = expression;
        while (current is ParenthesizedExpressionSyntax parens)
        {
            current = parens.Expression;
        }

        return current switch
        {
            CollectionExpressionSyntax { Elements.Count: 0 } => true,
            ObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 0, Initializer.Expressions.Count: 0 } => true,
            InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Empty" } } => true,
            _ => false,
        };
    }

    // TMP2123 — catch swallows a cancellation.
    private static void AnalyzeCatch(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var catchClause = (CatchClauseSyntax)context.Node;
        if (!state.IsWorkflowReachable(catchClause, context.SemanticModel))
        {
            return;
        }

        if (!IsBroadCatch(catchClause, context.SemanticModel))
        {
            return;
        }

        if (RethrowsOrChecksCancellation(catchClause))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.SwallowedCancellation,
            catchClause.CatchKeyword.GetLocation()));
    }

    private static bool IsBroadCatch(CatchClauseSyntax catchClause, SemanticModel model)
    {
        if (catchClause.Declaration is null)
        {
            return true;
        }

        var type = model.GetTypeInfo(catchClause.Declaration.Type).Type;
        return type is not null &&
               BroadCatchTypes.Contains(TypeNames.FullName(type));
    }

    private static bool RethrowsOrChecksCancellation(CatchClauseSyntax catchClause)
    {
        foreach (var node in catchClause.Block.DescendantNodesAndSelf())
        {
            if (node is ThrowStatementSyntax { Expression: null })
            {
                return true;
            }

            if (node is IdentifierNameSyntax { Identifier.ValueText: "IsCancellationRequested" })
            {
                return true;
            }
        }

        return false;
    }

    // TMP2124 — cleanup (finally) awaits outside a non-cancellable scope.
    private static void AnalyzeFinally(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var finallyClause = (FinallyClauseSyntax)context.Node;
        if (!state.IsWorkflowReachable(finallyClause, context.SemanticModel))
        {
            return;
        }

        if (!finallyClause.Block.DescendantNodes().OfType<AwaitExpressionSyntax>().Any())
        {
            return;
        }

        if (finallyClause.Block.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Any(id => id.Identifier.ValueText == "NonCancellableAsync"))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.CleanupNotNonCancellable,
            finallyClause.FinallyKeyword.GetLocation()));
    }

    // TMP2125 — unbounded loop in [WorkflowRun] that never checks continue-as-new.
    private static void AnalyzeLoop(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var node = context.Node;
        if (!IsUnbounded(node))
        {
            return;
        }

        if (GetEnclosingRunMethod(context, node) is not { } runMethod)
        {
            return;
        }

        var body = node switch
        {
            WhileStatementSyntax whileStatement => whileStatement.Statement,
            ForStatementSyntax forStatement => forStatement.Statement,
            _ => node,
        };

        if (ReferencesContinueAsNew(body))
        {
            return;
        }

        var keyword = node switch
        {
            WhileStatementSyntax whileStatement => whileStatement.WhileKeyword,
            ForStatementSyntax forStatement => forStatement.ForKeyword,
            _ => default,
        };

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.LongRunningLoopWithoutContinueAsNew,
            keyword.GetLocation()));
    }

    private static bool IsUnbounded(SyntaxNode node) => node switch
    {
        WhileStatementSyntax { Condition: LiteralExpressionSyntax literal } =>
            literal.IsKind(SyntaxKind.TrueLiteralExpression),
        ForStatementSyntax { Condition: null } => true,
        _ => false,
    };

    private static bool ReferencesContinueAsNew(SyntaxNode body)
    {
        foreach (var identifier in body.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
        {
            var name = identifier.Identifier.ValueText;
            if (name is "ContinueAsNewSuggested" or "CreateContinueAsNewException")
            {
                return true;
            }
        }

        return false;
    }

    private static IMethodSymbol? GetEnclosingRunMethod(SyntaxNodeAnalysisContext context, SyntaxNode node)
    {
        var enclosing = context.SemanticModel.GetEnclosingSymbol(node.SpanStart);
        for (var current = enclosing; current is not null; current = current.ContainingSymbol)
        {
            if (current is IMethodSymbol { MethodKind: not (MethodKind.LambdaMethod or MethodKind.LocalFunction) } method)
            {
                return WorkflowDetection.IsWorkflowRunMethod(method) ? method : null;
            }
        }

        return null;
    }

    private static bool IsWorkflowApi(IMethodSymbol method, string name) =>
        method.Name == name &&
        method.ContainingType is not null &&
        SdkNames.IsWorkflowType(method.ContainingType);
}
