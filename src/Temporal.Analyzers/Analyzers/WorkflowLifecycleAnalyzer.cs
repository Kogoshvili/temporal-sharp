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
/// (TMP2122), swallowed continue-as-new (TMP2123), cancellable cleanup (TMP2124),
/// and unbounded loops that never continue-as-new (TMP2125).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WorkflowLifecycleAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.ContinueAsNewWithoutState,
            DiagnosticDescriptors.SwallowedContinueAsNew,
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
                c => AnalyzeCancellationCatch(c, state),
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

        if (HasNoStateArgument(invocation, context.SemanticModel, method))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ContinueAsNewWithoutState,
                invocation.GetLocation()));
        }
    }

    private static bool HasNoStateArgument(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        IMethodSymbol method)
    {
        // Lambda overload: the state is whatever the lambda passes to the
        // workflow run method; the options argument is irrelevant.
        var lambda = invocation.ArgumentList.Arguments
            .Select(a => Unwrap(a.Expression))
            .OfType<LambdaExpressionSyntax>()
            .FirstOrDefault();

        if (lambda is not null)
        {
            return LambdaPassesNoState(lambda, model);
        }

        // String overload: the state is the args collection argument; a null or
        // empty *options* argument must not count as "no state".
        var state = FindStateArgument(invocation, method);
        return state is not null && (IsNullLiteral(state) || IsEmptyCollection(state));
    }

    private static bool LambdaPassesNoState(LambdaExpressionSyntax lambda, SemanticModel model)
    {
        var body = lambda.Body;
        while (body is ParenthesizedExpressionSyntax parens)
        {
            body = parens.Expression;
        }

        if (body is not InvocationExpressionSyntax invocation)
        {
            return false;
        }

        var target = model.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
        if (target is null || target.Parameters.Length == 0)
        {
            return true;
        }

        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            if (IsNullLiteral(argument.Expression) || IsEmptyCollection(argument.Expression))
            {
                return true;
            }
        }

        return false;
    }

    private static ExpressionSyntax? FindStateArgument(InvocationExpressionSyntax invocation, IMethodSymbol method)
    {
        // The state is the args collection; skip the workflow name (string) and
        // the trailing options argument. Note string itself implements
        // IEnumerable<char>, so a plain "is IEnumerable" check would mis-select
        // the workflow name.
        for (var i = 0; i < invocation.ArgumentList.Arguments.Count && i < method.Parameters.Length; i++)
        {
            var typeName = TypeNames.FullName(method.Parameters[i].Type);
            if (typeName is "System.String" or "Temporalio.Workflows.ContinueAsNewOptions")
            {
                continue;
            }

            return invocation.ArgumentList.Arguments[i].Expression;
        }

        return null;
    }

    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        var current = expression;
        while (current is CastExpressionSyntax cast)
        {
            current = cast.Expression;
        }

        while (current is ParenthesizedExpressionSyntax parens)
        {
            current = parens.Expression;
        }

        return current;
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

    // TMP2123 — catch swallows a continue-as-new exception.
    private static void AnalyzeCatch(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var catchClause = (CatchClauseSyntax)context.Node;
        if (!state.IsWorkflowReachable(catchClause, context.SemanticModel))
        {
            return;
        }

        if (!CanCatchContinueAsNew(catchClause, context.SemanticModel))
        {
            return;
        }

        if (RethrowsCaughtException(catchClause))
        {
            return;
        }

        if (!TryThrowsContinueAsNew(catchClause, context.SemanticModel))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.SwallowedContinueAsNew,
            catchClause.CatchKeyword.GetLocation()));
    }

    private static bool CanCatchContinueAsNew(CatchClauseSyntax catchClause, SemanticModel model)
    {
        if (catchClause.Declaration is null)
        {
            return true;
        }

        var type = model.GetTypeInfo(catchClause.Declaration.Type).Type;
        return type is not null &&
               TypeNames.FullName(type) is "System.Exception" or
                   "Temporalio.Exceptions.TemporalException" or
                   "Temporalio.Workflows.ContinueAsNewException";
    }

    private static bool RethrowsCaughtException(CatchClauseSyntax catchClause)
    {
        var catchVariable = catchClause.Declaration?.Identifier.ValueText;

        foreach (var node in catchClause.Block.DescendantNodesAndSelf())
        {
            if (node is ThrowStatementSyntax { Expression: null })
            {
                return true;
            }

            if (catchVariable is not null &&
                node is ThrowStatementSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: var rethrown } } &&
                rethrown == catchVariable)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryThrowsContinueAsNew(CatchClauseSyntax catchClause, SemanticModel model)
    {
        if (catchClause.Parent is not TryStatementSyntax tryStatement)
        {
            return false;
        }

        foreach (var throwStatement in tryStatement.Block.DescendantNodesAndSelf().OfType<ThrowStatementSyntax>())
        {
            if (throwStatement.Expression is null)
            {
                continue;
            }

            if (throwStatement.Expression is InvocationExpressionSyntax invocation &&
                model.GetSymbolInfo(invocation).Symbol is IMethodSymbol method &&
                IsWorkflowApi(method, "CreateContinueAsNewException"))
            {
                return true;
            }

            if (model.GetTypeInfo(throwStatement.Expression).Type is { } thrownType &&
                TypeNames.IsOrDerivesFrom(thrownType, "Temporalio.Workflows.ContinueAsNewException"))
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

        if (UsesNonCancellableToken(finallyClause.Block, context.SemanticModel))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.CleanupNotNonCancellable,
            finallyClause.FinallyKeyword.GetLocation()));
    }

    // TMP2124 — a cancellation catch performs cleanup with a cancellable token.
    private static void AnalyzeCancellationCatch(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var catchClause = (CatchClauseSyntax)context.Node;
        if (!state.IsWorkflowReachable(catchClause, context.SemanticModel))
        {
            return;
        }

        if (!IsCancellationCatch(catchClause, context.SemanticModel))
        {
            return;
        }

        if (!catchClause.Block.DescendantNodes().OfType<AwaitExpressionSyntax>().Any())
        {
            return;
        }

        if (UsesNonCancellableToken(catchClause.Block, context.SemanticModel))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.CleanupNotNonCancellable,
            catchClause.CatchKeyword.GetLocation()));
    }

    private static bool IsCancellationCatch(CatchClauseSyntax catchClause, SemanticModel model)
    {
        if (catchClause.Filter is { } filter &&
            ReferencesCancellationInFilter(filter.FilterExpression))
        {
            return true;
        }

        if (catchClause.Declaration is null)
        {
            return false;
        }

        var type = model.GetTypeInfo(catchClause.Declaration.Type).Type;
        return type is not null &&
               TypeNames.FullName(type) is "System.OperationCanceledException" or
                   "System.Threading.Tasks.TaskCanceledException";
    }

    private static bool ReferencesCancellationInFilter(ExpressionSyntax expression)
    {
        foreach (var name in expression.DescendantNodesAndSelf().OfType<SimpleNameSyntax>())
        {
            if (name.Identifier.ValueText is "IsCanceledException" or "IsCancellationRequested")
            {
                return true;
            }
        }

        return false;
    }

    private static bool UsesNonCancellableToken(BlockSyntax block, SemanticModel model)
    {
        foreach (var node in block.DescendantNodes())
        {
            if (node is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "None" } access &&
                model.GetSymbolInfo(access).Symbol is { Name: "None" } symbol &&
                symbol.ContainingType?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) ==
                SdkNames.CancellationTokenType)
            {
                return true;
            }

            // A fresh CancellationTokenSource created inside cleanup is a detached
            // token (the docs-sanctioned alternative to CancellationToken.None).
            if (node is BaseObjectCreationExpressionSyntax creation &&
                model.GetTypeInfo(creation).Type is { } type &&
                TypeNames.FullName(type) == "System.Threading.CancellationTokenSource")
            {
                return true;
            }
        }

        return false;
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

        if (HasTerminatingStatement(body) || ReferencesWaitCondition(body))
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
        // SimpleNameSyntax covers both IdentifierNameSyntax and GenericNameSyntax
        // (e.g. CreateContinueAsNewException<T>).
        foreach (var name in body.DescendantNodesAndSelf().OfType<SimpleNameSyntax>())
        {
            var text = name.Identifier.ValueText;
            if (text is "ContinueAsNewSuggested" or "CreateContinueAsNewException")
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasTerminatingStatement(SyntaxNode body)
    {
        foreach (var statement in body.DescendantNodesAndSelf())
        {
            if (statement is BreakStatementSyntax or ReturnStatementSyntax or ThrowStatementSyntax)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ReferencesWaitCondition(SyntaxNode body)
    {
        foreach (var invocation in body.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is MemberAccessExpressionSyntax member &&
                member.Name.Identifier.ValueText == "WaitConditionAsync")
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
