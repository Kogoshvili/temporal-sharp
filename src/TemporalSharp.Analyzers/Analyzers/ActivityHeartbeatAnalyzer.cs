using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using TemporalSharp.Analyzers.Analysis;
using TemporalSharp.Analyzers.Diagnostics;

namespace TemporalSharp.Analyzers.Analyzers;

/// <summary>
/// Flags activities that should heartbeat but never call
/// <c>ActivityExecutionContext.Heartbeat()</c>: long-running activities (loops or
/// multiple awaits) and activities invoked with a heartbeat timeout.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ActivityHeartbeatAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.ActivityNeverHeartbeats,
            DiagnosticDescriptors.HeartbeatTimeoutWithoutHeartbeat);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
        {
            var state = new HeartbeatState();

            startContext.RegisterSymbolAction(
                symbolContext => CollectLongRunningActivity(symbolContext, state),
                SymbolKind.Method);

            startContext.RegisterSyntaxNodeAction(
                nodeContext => CollectHeartbeatCall(nodeContext, state),
                SyntaxKind.InvocationExpression);

            startContext.RegisterSyntaxNodeAction(
                nodeContext => CollectHeartbeatTimeoutCandidate(nodeContext, state),
                SyntaxKind.ObjectCreationExpression);

            startContext.RegisterCompilationEndAction(endContext => Report(endContext, state));
        });
    }

    private static void CollectLongRunningActivity(SymbolAnalysisContext context, HeartbeatState state)
    {
        var method = (IMethodSymbol)context.Symbol;
        if (!WorkflowDetection.IsActivityMethod(method))
        {
            return;
        }

        if (!HasLoopOrMultipleAwaits(method))
        {
            return;
        }

        state.LongRunningActivities.TryAdd(method, 0);
    }

    private static void CollectHeartbeatCall(SyntaxNodeAnalysisContext context, HeartbeatState state)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol target ||
            target.Name != "Heartbeat" ||
            target.ContainingType?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) !=
            SdkNames.ActivityExecutionContextType)
        {
            return;
        }

        var enclosing = context.SemanticModel.GetEnclosingSymbol(invocation.SpanStart);
        for (var current = enclosing; current is not null; current = current.ContainingSymbol)
        {
            if (current is IMethodSymbol method && WorkflowDetection.IsActivityMethod(method))
            {
                state.HeartbeatingActivities.TryAdd(method, 0);
                return;
            }
        }
    }

    private static void CollectHeartbeatTimeoutCandidate(SyntaxNodeAnalysisContext context, HeartbeatState state)
    {
        var creation = (ObjectCreationExpressionSyntax)context.Node;
        var type = context.SemanticModel.GetTypeInfo(creation).Type;
        if (type is null)
        {
            return;
        }

        var typeName = type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        if (typeName != SdkNames.ActivityOptionsType && typeName != SdkNames.LocalActivityOptionsType)
        {
            return;
        }

        if (creation.Initializer is null ||
            !creation.Initializer.Expressions
                .OfType<AssignmentExpressionSyntax>()
                .Any(a => a.Left is IdentifierNameSyntax id && id.Identifier.ValueText == "HeartbeatTimeout"))
        {
            return;
        }

        var invocation = FindEnclosingInvocation(creation);
        if (invocation is null)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol apiMethod ||
            apiMethod.ContainingType?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) != SdkNames.WorkflowType ||
            apiMethod.Name is not ("ExecuteActivityAsync" or "ExecuteLocalActivityAsync"))
        {
            return;
        }

        var activityMethod = ResolveTypedLambdaTarget(context, invocation);
        if (activityMethod is not null && WorkflowDetection.IsActivityMethod(activityMethod))
        {
            state.HeartbeatTimeoutCandidates.Add((activityMethod, creation.GetLocation()));
        }
    }

    private static void Report(CompilationAnalysisContext context, HeartbeatState state)
    {
        foreach (var method in state.LongRunningActivities.Keys)
        {
            if (state.HeartbeatingActivities.ContainsKey(method))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ActivityNeverHeartbeats,
                FirstLocation(method),
                method.Name));
        }

        foreach (var (method, location) in state.HeartbeatTimeoutCandidates)
        {
            if (state.HeartbeatingActivities.ContainsKey(method))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.HeartbeatTimeoutWithoutHeartbeat,
                location,
                method.Name));
        }
    }

    private static bool HasLoopOrMultipleAwaits(IMethodSymbol method)
    {
        foreach (var syntaxReference in method.DeclaringSyntaxReferences)
        {
            var node = syntaxReference.GetSyntax();
            if (node.DescendantNodes().Any(n => n is ForStatementSyntax or ForEachStatementSyntax or WhileStatementSyntax or DoStatementSyntax))
            {
                return true;
            }

            if (node.DescendantNodes().OfType<AwaitExpressionSyntax>().Count() >= 2)
            {
                return true;
            }
        }

        return false;
    }

    private static InvocationExpressionSyntax? FindEnclosingInvocation(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is InvocationExpressionSyntax invocation)
            {
                return invocation;
            }

            if (current is StatementSyntax or MemberDeclarationSyntax)
            {
                return null;
            }
        }

        return null;
    }

    private static IMethodSymbol? ResolveTypedLambdaTarget(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation)
    {
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            var expression = argument.Expression;
            while (expression is CastExpressionSyntax cast)
            {
                expression = cast.Expression;
            }

            if (expression is not LambdaExpressionSyntax lambda)
            {
                continue;
            }

            var body = lambda.Body;
            while (body is ParenthesizedExpressionSyntax parens)
            {
                body = parens.Expression;
            }

            if (body is InvocationExpressionSyntax bodyInvocation)
            {
                return context.SemanticModel.GetSymbolInfo(bodyInvocation).Symbol as IMethodSymbol;
            }
        }

        return null;
    }

    private static Location FirstLocation(IMethodSymbol method) =>
        method.Locations.Length > 0 ? method.Locations[0] : Location.None;

    private sealed class HeartbeatState
    {
        public ConcurrentDictionary<IMethodSymbol, byte> LongRunningActivities { get; } = new(SymbolEqualityComparer.Default);

        public ConcurrentDictionary<IMethodSymbol, byte> HeartbeatingActivities { get; } = new(SymbolEqualityComparer.Default);

        public ConcurrentBag<(IMethodSymbol Method, Location Location)> HeartbeatTimeoutCandidates { get; } = new();
    }
}
