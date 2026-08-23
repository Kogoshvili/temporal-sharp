using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Kogoshvili.Temporal.Analyzers.Analysis;
using Kogoshvili.Temporal.Analyzers.Diagnostics;

namespace Kogoshvili.Temporal.Analyzers.Analyzers;

/// <summary>
/// Validates the heartbeat contract of activities:
/// <list type="bullet">
/// <item>TMP3101 — long-running activity (loop or multiple awaits) never heartbeats.</item>
/// <item>TMP3102 — activity invoked with <c>HeartbeatTimeout</c> never heartbeats (error).</item>
/// <item>TMP3103 — activity heartbeats but is invoked without a <c>HeartbeatTimeout</c>.</item>
/// <item>TMP3104 — activity heartbeats but is not long-running (heartbeat unnecessary).</item>
/// </list>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ActivityHeartbeatAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.ActivityNeverHeartbeats,
            DiagnosticDescriptors.HeartbeatTimeoutWithoutHeartbeat,
            DiagnosticDescriptors.HeartbeatWithoutTimeout,
            DiagnosticDescriptors.UnnecessaryHeartbeat,
            DiagnosticDescriptors.HeartbeatTimeoutMismatch,
            DiagnosticDescriptors.HeartbeatWithoutCancellationCheck);

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
                nodeContext => CollectOptionsObjectCreation(nodeContext, state),
                SyntaxKind.ObjectCreationExpression);

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeTimeoutRatio(nodeContext, state),
                SyntaxKind.ObjectCreationExpression);

            startContext.RegisterSyntaxNodeAction(
                nodeContext => CollectActivityInvocation(nodeContext, state),
                SyntaxKind.InvocationExpression);

            startContext.RegisterCompilationEndAction(endContext => Report(endContext, state));
        });
    }

    // TMP3108 — HeartbeatTimeout much shorter than StartToCloseTimeout.
    private static void AnalyzeTimeoutRatio(SyntaxNodeAnalysisContext context, HeartbeatState state)
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

        if (creation.Initializer is not { } initializer)
        {
            return;
        }

        ExpressionSyntax? heartbeat = null;
        ExpressionSyntax? startToClose = null;
        foreach (var assignment in initializer.Expressions.OfType<AssignmentExpressionSyntax>())
        {
            if (assignment.Left is not IdentifierNameSyntax identifier)
            {
                continue;
            }

            switch (identifier.Identifier.ValueText)
            {
                case "HeartbeatTimeout":
                    heartbeat = assignment.Right;
                    break;
                case "StartToCloseTimeout":
                    startToClose = assignment.Right;
                    break;
            }
        }

        var heartbeatTicks = heartbeat is null ? null : EvaluateTimeSpanTicks(heartbeat);
        var startToCloseTicks = startToClose is null ? null : EvaluateTimeSpanTicks(startToClose);

        if (heartbeatTicks is null || startToCloseTicks is null)
        {
            return;
        }

        // Heartbeat far shorter than the total budget is a smell (heuristic).
        if (heartbeatTicks.Value * 10 < startToCloseTicks.Value)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.HeartbeatTimeoutMismatch,
                heartbeat!.GetLocation()));
        }
    }

    private static long? EvaluateTimeSpanTicks(ExpressionSyntax expression)
    {
        var current = Unwrap(expression);
        if (current is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax member } invocation)
        {
            return null;
        }

        long? multiplier = member.Name.Identifier.ValueText switch
        {
            "FromMilliseconds" => TimeSpan.TicksPerMillisecond,
            "FromSeconds" => TimeSpan.TicksPerSecond,
            "FromMinutes" => TimeSpan.TicksPerMinute,
            "FromHours" => TimeSpan.TicksPerHour,
            "FromDays" => TimeSpan.TicksPerDay,
            "FromTicks" => 1,
            _ => null,
        };

        if (multiplier is null)
        {
            return null;
        }

        var argument = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
        if (argument is null || !TryEvaluateNumber(argument, out var value))
        {
            return null;
        }

        return (long)(value * multiplier.Value);
    }

    private static bool TryEvaluateNumber(ExpressionSyntax expression, out double value)
    {
        var current = Unwrap(expression);
        if (current is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NumericLiteralExpression } literal &&
            double.TryParse(literal.Token.ValueText, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        if (current is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.UnaryMinusExpression } negated &&
            TryEvaluateNumber(negated.Operand, out value))
        {
            value = -value;
            return true;
        }

        value = 0;
        return false;
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

    private static void CollectOptionsObjectCreation(SyntaxNodeAnalysisContext context, HeartbeatState state)
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

        var hasTimeout = InitializerHasHeartbeatTimeout(creation.Initializer);

        if (creation.Parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator })
        {
            var symbol = context.SemanticModel.GetDeclaredSymbol(declarator);
            if (symbol is not null)
            {
                state.OptionsStatus[symbol] = hasTimeout;
            }
        }
    }

    private static void CollectActivityInvocation(SyntaxNodeAnalysisContext context, HeartbeatState state)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol apiMethod ||
            apiMethod.ContainingType?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) != SdkNames.WorkflowType ||
            apiMethod.Name is not ("ExecuteActivityAsync" or "ExecuteLocalActivityAsync"))
        {
            return;
        }

        var activityMethod = LambdaTargetResolver.ResolveTypedLambdaTarget(context, invocation);
        if (activityMethod is null || !WorkflowDetection.IsActivityMethod(activityMethod))
        {
            return;
        }

        var options = FindOptionsArgument(context, invocation);
        if (options is null)
        {
            return;
        }

        var location = invocation.GetLocation();
        var unwrapped = Unwrap(options);

        if (unwrapped is ObjectCreationExpressionSyntax creation)
        {
            if (InitializerHasHeartbeatTimeout(creation.Initializer))
            {
                state.TimeoutSet[activityMethod] = location;
            }
            else
            {
                state.TimeoutNotSet[activityMethod] = location;
            }

            return;
        }

        if (unwrapped is IdentifierNameSyntax identifier)
        {
            var symbol = context.SemanticModel.GetSymbolInfo(identifier).Symbol;
            if (symbol is not null)
            {
                state.PendingOptionSymbols.Add((activityMethod, location, symbol));
            }
        }
    }

    private static void Report(CompilationAnalysisContext context, HeartbeatState state)
    {
        ResolvePendingOptions(state);

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

        foreach (var pair in state.TimeoutSet)
        {
            if (state.HeartbeatingActivities.ContainsKey(pair.Key))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.HeartbeatTimeoutWithoutHeartbeat,
                pair.Value,
                pair.Key.Name));
        }

        foreach (var pair in state.TimeoutNotSet)
        {
            if (!state.HeartbeatingActivities.ContainsKey(pair.Key) || state.TimeoutSet.ContainsKey(pair.Key))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.HeartbeatWithoutTimeout,
                pair.Value,
                pair.Key.Name));
        }

        foreach (var method in state.HeartbeatingActivities.Keys)
        {
            if (state.LongRunningActivities.ContainsKey(method))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.UnnecessaryHeartbeat,
                FirstLocation(method),
                method.Name));
        }

        // TMP3109 — activity heartbeats in a loop but never checks the cancellation token.
        foreach (var method in state.HeartbeatingActivities.Keys)
        {
            if (!state.LongRunningActivities.ContainsKey(method) ||
                ReferencesCancellationCheck(method))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.HeartbeatWithoutCancellationCheck,
                FirstLocation(method),
                method.Name));
        }
    }

    private static bool ReferencesCancellationCheck(IMethodSymbol method)
    {
        foreach (var syntaxReference in method.DeclaringSyntaxReferences)
        {
            foreach (var identifier in syntaxReference.GetSyntax().DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                if (identifier.Identifier.ValueText.IndexOf("Cancellation", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void ResolvePendingOptions(HeartbeatState state)
    {
        foreach (var (method, location, symbol) in state.PendingOptionSymbols)
        {
            if (!state.OptionsStatus.TryGetValue(symbol, out var hasTimeout))
            {
                continue;
            }

            if (hasTimeout)
            {
                state.TimeoutSet[method] = location;
            }
            else
            {
                state.TimeoutNotSet[method] = location;
            }
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

    private static bool InitializerHasHeartbeatTimeout(InitializerExpressionSyntax? initializer)
    {
        if (initializer is null)
        {
            return false;
        }

        return initializer.Expressions
            .OfType<AssignmentExpressionSyntax>()
            .Any(a => a.Left is IdentifierNameSyntax id && id.Identifier.ValueText == "HeartbeatTimeout");
    }

    private static ExpressionSyntax? FindOptionsArgument(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
    {
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            var type = context.SemanticModel.GetTypeInfo(argument.Expression).Type;
            if (type is null)
            {
                continue;
            }

            var typeName = type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
            if (typeName == SdkNames.ActivityOptionsType || typeName == SdkNames.LocalActivityOptionsType)
            {
                return argument.Expression;
            }
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

    private static Location FirstLocation(IMethodSymbol method) =>
        method.Locations.Length > 0 ? method.Locations[0] : Location.None;

    private sealed class HeartbeatState
    {
        public ConcurrentDictionary<IMethodSymbol, byte> LongRunningActivities { get; } = new(SymbolEqualityComparer.Default);

        public ConcurrentDictionary<IMethodSymbol, byte> HeartbeatingActivities { get; } = new(SymbolEqualityComparer.Default);

        public ConcurrentDictionary<IMethodSymbol, Location> TimeoutSet { get; } = new(SymbolEqualityComparer.Default);

        public ConcurrentDictionary<IMethodSymbol, Location> TimeoutNotSet { get; } = new(SymbolEqualityComparer.Default);

        public ConcurrentDictionary<ISymbol, bool> OptionsStatus { get; } = new(SymbolEqualityComparer.Default);

        public ConcurrentBag<(IMethodSymbol Method, Location Location, ISymbol Symbol)> PendingOptionSymbols { get; } = new();
    }
}
