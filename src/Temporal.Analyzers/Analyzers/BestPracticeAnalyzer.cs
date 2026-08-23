using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Kogoshvili.Temporal.Analyzers.Analysis;
using Kogoshvili.Temporal.Analyzers.Diagnostics;

namespace Kogoshvili.Temporal.Analyzers.Analyzers;

/// <summary>
/// Flags best-practice deviations: multiple positional parameters (TMP4101),
/// polling loops (TMP4103), CPU-heavy loops (TMP4104), hard-coded task queues
/// (TMP4105), consecutive local activities (TMP4106), and blocking I/O in local
/// activities (TMP4107).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BestPracticeAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> BlockingIoMembers = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "System.Threading.Tasks.Task.Delay",
        "System.Threading.Thread.Sleep");

    private static readonly ImmutableHashSet<string> BlockingIoTypes = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "System.Net.Http.HttpClient",
        "System.Net.Sockets.Socket",
        "System.Net.Sockets.TcpClient",
        "System.Net.Sockets.UdpClient",
        "System.Net.Sockets.NetworkStream",
        "System.IO.File",
        "System.IO.FileStream",
        "System.IO.Directory",
        "System.IO.DirectoryInfo",
        "System.IO.FileInfo",
        "System.IO.StreamWriter",
        "System.IO.StreamReader");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.MultipleParameters,
            DiagnosticDescriptors.PollingLoop,
            DiagnosticDescriptors.HeavyCpuLoop,
            DiagnosticDescriptors.HardcodedTaskQueue,
            DiagnosticDescriptors.ConsecutiveLocalActivities,
            DiagnosticDescriptors.LocalActivityBlockingIo);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
        {
            var state = CompilationAnalysisState.Get(startContext.Compilation, startContext.Options);
            var localActivityIndex = LocalActivityIndex.Get(startContext.Compilation);

            startContext.RegisterSymbolAction(
                AnalyzeMethodSignature,
                SymbolKind.Method);

            startContext.RegisterSyntaxNodeAction(
                c => AnalyzePollingLoop(c, state),
                SyntaxKind.WhileStatement,
                SyntaxKind.ForStatement);

            startContext.RegisterSyntaxNodeAction(
                c => AnalyzeHeavyCpuLoop(c, state),
                SyntaxKind.WhileStatement,
                SyntaxKind.ForStatement,
                SyntaxKind.ForEachStatement);

            startContext.RegisterSyntaxNodeAction(
                c => AnalyzeTaskQueue(c),
                SyntaxKind.ObjectCreationExpression,
                SyntaxKind.ImplicitObjectCreationExpression);

            startContext.RegisterSyntaxNodeAction(
                c => AnalyzeConsecutiveLocalActivities(c, state),
                SyntaxKind.InvocationExpression);

            startContext.RegisterSyntaxNodeAction(
                c => AnalyzeLocalActivityIo(c, localActivityIndex),
                SyntaxKind.InvocationExpression);
        });
    }

    // TMP4101 — prefer a single object parameter over many positional parameters.
    private static void AnalyzeMethodSignature(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;
        if (!IsContractMethod(method))
        {
            return;
        }

        var businessParams = 0;
        foreach (var parameter in method.Parameters)
        {
            if (!IsIgnoredParameter(parameter))
            {
                businessParams++;
            }
        }

        if (businessParams < 2)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.MultipleParameters,
            method.Locations[0],
            method.Name,
            businessParams));
    }

    private static bool IsContractMethod(IMethodSymbol method) =>
        WorkflowDetection.IsWorkflowRunMethod(method) ||
        WorkflowDetection.IsWorkflowQueryMethod(method) ||
        WorkflowDetection.IsWorkflowSignalMethod(method) ||
        WorkflowDetection.IsWorkflowUpdateMethod(method) ||
        WorkflowDetection.IsActivityMethod(method);

    private static bool IsIgnoredParameter(IParameterSymbol parameter)
    {
        if (TypeNames.FullName(parameter.Type) == "System.Threading.CancellationToken")
        {
            return true;
        }

        return parameter.Name.IndexOf("idempotency", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // TMP4103 — polling loop that awaits a constant Workflow.DelayAsync.
    private static void AnalyzePollingLoop(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var node = context.Node;
        if (!state.IsWorkflowReachable(node, context.SemanticModel))
        {
            return;
        }

        var body = node switch
        {
            WhileStatementSyntax whileStatement => whileStatement.Statement,
            ForStatementSyntax forStatement => forStatement.Statement,
            _ => node,
        };

        if (!ContainsConstantDelayAwait(body, context.SemanticModel))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.PollingLoop,
            LoopKeyword(node).GetLocation(),
            "loop"));
    }

    // TMP4104 — loop in workflow code with no await (CPU-heavy).
    private static void AnalyzeHeavyCpuLoop(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var node = context.Node;
        if (!state.IsWorkflowReachable(node, context.SemanticModel))
        {
            return;
        }

        var body = node switch
        {
            WhileStatementSyntax whileStatement => whileStatement.Statement,
            ForStatementSyntax forStatement => forStatement.Statement,
            ForEachStatementSyntax forEachStatement => forEachStatement.Statement,
            _ => node,
        };

        if (body.DescendantNodesAndSelf().OfType<AwaitExpressionSyntax>().Any())
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.HeavyCpuLoop,
            LoopKeyword(node).GetLocation(),
            "loop"));
    }

    // TMP4105 — hard-coded task-queue name on a Temporal options type. Applies to
    // any Temporalio.*Options construction (worker, starter, or activity options),
    // not just workflow-reachable code, since task-queue strings typically live in
    // worker/starter setup.
    private static void AnalyzeTaskQueue(SyntaxNodeAnalysisContext context)
    {
        var creation = (BaseObjectCreationExpressionSyntax)context.Node;
        var type = context.SemanticModel.GetTypeInfo(creation).Type;
        if (type is null || !IsTemporalOptionType(type))
        {
            return;
        }

        // Object initializer: TaskQueue = "literal".
        if (creation.Initializer is { } initializer)
        {
            foreach (var expression in initializer.Expressions)
            {
                if (expression is not AssignmentExpressionSyntax
                    {
                        Left: IdentifierNameSyntax { Identifier.ValueText: "TaskQueue" },
                        Right: LiteralExpressionSyntax literal,
                    } assignment ||
                    !literal.IsKind(SyntaxKind.StringLiteralExpression))
                {
                    continue;
                }

                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.HardcodedTaskQueue,
                    assignment.GetLocation(),
                    literal.Token.ValueText));
            }
        }

        // Constructor argument: WorkflowOptions(taskQueue: "…") or the
        // TemporalWorkerOptions(string taskQueue) positional overload.
        if (creation.ArgumentList is not { } argumentList)
        {
            return;
        }

        var arguments = argumentList.Arguments;
        for (var i = 0; i < arguments.Count; i++)
        {
            var argument = arguments[i];

            var isTaskQueue = argument.NameColon?.Name.Identifier.ValueText is "TaskQueue" or "taskQueue";
            if (!isTaskQueue &&
                argument.NameColon is null &&
                i == 0 &&
                TypeNames.FullName(type) == "Temporalio.Worker.TemporalWorkerOptions")
            {
                isTaskQueue = true;
            }

            if (isTaskQueue &&
                argument.Expression is LiteralExpressionSyntax literal &&
                literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.HardcodedTaskQueue,
                    argument.GetLocation(),
                    literal.Token.ValueText));
            }
        }
    }

    // TMP4106 — two or more consecutive ExecuteLocalActivityAsync calls.
    private static void AnalyzeConsecutiveLocalActivities(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!IsLocalActivityInvocation(context.SemanticModel, invocation))
        {
            return;
        }

        if (!state.IsWorkflowReachable(invocation, context.SemanticModel))
        {
            return;
        }

        if (invocation.FirstAncestorOrSelf<StatementSyntax>() is not { } statement ||
            GetPreviousStatement(statement) is not { } previous ||
            !IsLocalActivityStatement(previous, context.SemanticModel))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.ConsecutiveLocalActivities,
            invocation.GetLocation()));
    }

    // TMP4107 — blocking/network I/O inside a local activity.
    private static void AnalyzeLocalActivityIo(SyntaxNodeAnalysisContext context, LocalActivityIndex index)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol called ||
            !IsBlockingIo(called))
        {
            return;
        }

        var enclosing = SymbolUtilities.GetEnclosingRegularMethod(
            context.SemanticModel.GetEnclosingSymbol(invocation.SpanStart));
        if (enclosing is null || !index.IsLocalActivity(enclosing))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.LocalActivityBlockingIo,
            invocation.GetLocation(),
            called.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
    }

    private static bool IsLocalActivityInvocation(SemanticModel model, InvocationExpressionSyntax invocation)
    {
        if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
        {
            return false;
        }

        return method.Name == "ExecuteLocalActivityAsync" &&
               method.ContainingType is not null &&
               SdkNames.IsWorkflowType(method.ContainingType);
    }

    private static bool IsBlockingIo(IMethodSymbol method)
    {
        if (BlockingIoMembers.Contains(SymbolKeys.Member(method)))
        {
            return true;
        }

        return method.ContainingType is not null &&
               BlockingIoTypes.Contains(TypeNames.FullName(method.ContainingType));
    }

    private static bool ContainsConstantDelayAwait(SyntaxNode body, SemanticModel model)
    {
        foreach (var node in body.DescendantNodesAndSelf())
        {
            if (node is not AwaitExpressionSyntax { Expression: InvocationExpressionSyntax invocation })
            {
                continue;
            }

            if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method ||
                method.ContainingType is null ||
                !SdkNames.IsWorkflowType(method.ContainingType) ||
                method.Name != "DelayAsync")
            {
                continue;
            }

            var argument = invocation.ArgumentList?.Arguments.FirstOrDefault()?.Expression;
            if (argument is not null && IsConstantDuration(argument))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsConstantDuration(ExpressionSyntax expression)
    {
        if (expression is LiteralExpressionSyntax)
        {
            return true;
        }

        return expression is InvocationExpressionSyntax
        {
            Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: { } name },
        } && name is "FromMilliseconds" or "FromSeconds" or "FromMinutes" or "FromHours" or "FromDays" or "FromTicks";
    }

    private static bool IsTemporalOptionType(ITypeSymbol type)
    {
        var ns = type.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        return ns.StartsWith("Temporalio", StringComparison.Ordinal) &&
               type.Name.EndsWith("Options", StringComparison.Ordinal);
    }

    private static bool IsLocalActivityStatement(StatementSyntax statement, SemanticModel model)
    {
        foreach (var node in statement.DescendantNodesAndSelf())
        {
            if (node is InvocationExpressionSyntax invocation && IsLocalActivityInvocation(model, invocation))
            {
                return true;
            }
        }

        return false;
    }

    private static StatementSyntax? GetPreviousStatement(StatementSyntax statement)
    {
        if (statement.Parent is not BlockSyntax block)
        {
            return null;
        }

        var index = block.Statements.IndexOf(statement);
        return index > 0 ? block.Statements[index - 1] : null;
    }

    private static SyntaxToken LoopKeyword(SyntaxNode node) => node switch
    {
        WhileStatementSyntax whileStatement => whileStatement.WhileKeyword,
        ForStatementSyntax forStatement => forStatement.ForKeyword,
        ForEachStatementSyntax forEachStatement => forEachStatement.ForEachKeyword,
        _ => default,
    };
}
