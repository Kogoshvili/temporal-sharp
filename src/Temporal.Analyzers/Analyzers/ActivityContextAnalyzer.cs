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
/// Flags activity execution-context misuse: capturing <c>ActivityExecutionContext</c>
/// across an await (TMP3105), logging to a non-SDK logger (TMP3106), and HTTP
/// calls without a <c>CancellationToken</c> (TMP3107).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ActivityContextAnalyzer : DiagnosticAnalyzer
{
    private static readonly SyntaxKind[] AssignmentKinds =
    {
        SyntaxKind.SimpleAssignmentExpression,
        SyntaxKind.AddAssignmentExpression,
        SyntaxKind.SubtractAssignmentExpression,
        SyntaxKind.MultiplyAssignmentExpression,
        SyntaxKind.DivideAssignmentExpression,
        SyntaxKind.ModuloAssignmentExpression,
        SyntaxKind.AndAssignmentExpression,
        SyntaxKind.OrAssignmentExpression,
        SyntaxKind.ExclusiveOrAssignmentExpression,
        SyntaxKind.LeftShiftAssignmentExpression,
        SyntaxKind.RightShiftAssignmentExpression,
        SyntaxKind.CoalesceAssignmentExpression,
    };

    private static readonly ImmutableHashSet<string> ConsoleLogMembers = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "System.Console.WriteLine",
        "System.Console.Write",
        // Console.Error is a TextWriter, so Console.Error.WriteLine binds to
        // TextWriter.WriteLine (not a System.Console member).
        "System.IO.TextWriter.WriteLine",
        "System.IO.TextWriter.Write",
        "System.Diagnostics.Debug.WriteLine",
        "System.Diagnostics.Debug.Write",
        "System.Diagnostics.Trace.WriteLine",
        "System.Diagnostics.Trace.Write");

    private static readonly ImmutableHashSet<string> HttpClientMethods = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "GetAsync", "PostAsync", "PutAsync", "DeleteAsync", "PatchAsync",
        "SendAsync", "GetStringAsync", "GetByteArrayAsync", "GetStreamAsync");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.ActivityContextAcrossAwait,
            DiagnosticDescriptors.NonSdkActivityLog,
            DiagnosticDescriptors.HttpClientWithoutCancellation);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
        {
            var state = new ActivityContextState();

            startContext.RegisterSyntaxNodeAction(
                c => CollectCapture(c, state),
                SyntaxKind.VariableDeclarator);

            startContext.RegisterSyntaxNodeAction(
                c => CollectCapture(c, state),
                AssignmentKinds);

            startContext.RegisterSyntaxNodeAction(
                c => CollectUse(c, state),
                SyntaxKind.IdentifierName);

            startContext.RegisterSyntaxNodeAction(
                c => CollectAwait(c, state),
                SyntaxKind.AwaitExpression);

            startContext.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);

            startContext.RegisterCompilationEndAction(endContext => ReportContextCapture(endContext, state));
        });
    }

    private static void CollectCapture(SyntaxNodeAnalysisContext context, ActivityContextState state)
    {
        ExpressionSyntax? value;
        ISymbol? captured;
        switch (context.Node)
        {
            case VariableDeclaratorSyntax declarator when declarator.Initializer is { } initializer:
                value = initializer.Value;
                captured = context.SemanticModel.GetDeclaredSymbol(declarator);
                break;

            case AssignmentExpressionSyntax assignment:
                value = assignment.Right;
                captured = context.SemanticModel.GetSymbolInfo(assignment.Left).Symbol;
                break;

            default:
                return;
        }

        if (captured is null ||
            !IsActivityContextCurrent(value, context.SemanticModel) ||
            GetEnclosingActivityMethod(context, context.Node) is not { } activityMethod)
        {
            return;
        }

        state.Captures[captured] = (value.GetLocation(), activityMethod);
    }

    private static void CollectUse(SyntaxNodeAnalysisContext context, ActivityContextState state)
    {
        var identifier = (IdentifierNameSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(identifier).Symbol is not { } symbol ||
            GetEnclosingActivityMethod(context, identifier) is not { } activityMethod)
        {
            return;
        }

        var uses = state.Uses.GetOrAdd(activityMethod, _ => new ConcurrentBag<(ISymbol Symbol, int SpanStart)>());
        uses.Add((symbol, identifier.SpanStart));
    }

    private static void CollectAwait(SyntaxNodeAnalysisContext context, ActivityContextState state)
    {
        var awaitExpression = (AwaitExpressionSyntax)context.Node;
        if (GetEnclosingActivityMethod(context, awaitExpression) is not { } activityMethod)
        {
            return;
        }

        state.FirstAwaitSpan.AddOrUpdate(
            activityMethod,
            awaitExpression.SpanStart,
            (_, existing) => Math.Min(existing, awaitExpression.SpanStart));
    }

    private static void ReportContextCapture(CompilationAnalysisContext context, ActivityContextState state)
    {
        foreach (var capture in state.Captures)
        {
            var symbol = capture.Key;
            var location = capture.Value.Location;
            var method = capture.Value.Method;

            // Capturing the context is only a problem when it is actually used
            // after an await; using it before the await is fine.
            if (!state.FirstAwaitSpan.TryGetValue(method, out var firstAwait))
            {
                continue;
            }

            if (!state.Uses.TryGetValue(method, out var uses) ||
                !uses.Any(u => SymbolEqualityComparer.Default.Equals(u.Symbol, symbol) && u.SpanStart > firstAwait))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ActivityContextAcrossAwait,
                location));
        }
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
        {
            return;
        }

        if (GetEnclosingActivityMethod(context, invocation) is not { } activityMethod)
        {
            return;
        }

        var memberKey = SymbolKeys.Member(method);
        var display = method.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

        if (ConsoleLogMembers.Contains(memberKey))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.NonSdkActivityLog,
                invocation.GetLocation(),
                display));
            return;
        }

        if (TypeNames.FullName(method.ContainingType) == "System.Net.Http.HttpClient" &&
            HttpClientMethods.Contains(method.Name) &&
            !HasCancellationTokenArgument(invocation, context.SemanticModel))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.HttpClientWithoutCancellation,
                invocation.GetLocation(),
                display));
        }
    }

    private static bool IsActivityContextCurrent(ExpressionSyntax expression, SemanticModel model)
    {
        if (expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        var symbol = model.GetSymbolInfo(memberAccess).Symbol;
        return symbol is IPropertySymbol { Name: "Current" } property &&
               property.ContainingType is not null &&
               property.ContainingType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) ==
               SdkNames.ActivityExecutionContextType;
    }

    private static bool HasCancellationTokenArgument(InvocationExpressionSyntax invocation, SemanticModel model)
    {
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            var type = model.GetTypeInfo(argument.Expression).Type;
            if (type is not null &&
                TypeNames.FullName(type) == "System.Threading.CancellationToken")
            {
                return true;
            }
        }

        return false;
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

    private sealed class ActivityContextState
    {
        public ConcurrentDictionary<ISymbol, (Location Location, IMethodSymbol Method)> Captures { get; } =
            new(SymbolEqualityComparer.Default);

        public ConcurrentDictionary<IMethodSymbol, ConcurrentBag<(ISymbol Symbol, int SpanStart)>> Uses { get; } =
            new(SymbolEqualityComparer.Default);

        public ConcurrentDictionary<IMethodSymbol, int> FirstAwaitSpan { get; } =
            new(SymbolEqualityComparer.Default);
    }
}
