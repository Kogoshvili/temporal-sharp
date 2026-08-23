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
        "System.Console.Error.WriteLine",
        "System.Console.Error.Write",
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

        context.RegisterSyntaxNodeAction(AnalyzeAssignment, AssignmentKinds);
        context.RegisterSyntaxNodeAction(AnalyzeVariableDeclarator, SyntaxKind.VariableDeclarator);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;
        ReportIfContextCaptured(context, assignment.Right);
    }

    private static void AnalyzeVariableDeclarator(SyntaxNodeAnalysisContext context)
    {
        var declarator = (VariableDeclaratorSyntax)context.Node;
        if (declarator.Initializer is { } initializer)
        {
            ReportIfContextCaptured(context, initializer.Value);
        }
    }

    private static void ReportIfContextCaptured(SyntaxNodeAnalysisContext context, ExpressionSyntax value)
    {
        if (!IsActivityContextCurrent(value, context.SemanticModel))
        {
            return;
        }

        if (GetEnclosingActivityMethod(context, value) is not { } activityMethod ||
            !HasAwait(activityMethod))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.ActivityContextAcrossAwait,
            value.GetLocation()));
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

    private static bool HasAwait(IMethodSymbol method)
    {
        foreach (var syntaxReference in method.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax().DescendantNodes().OfType<AwaitExpressionSyntax>().Any())
            {
                return true;
            }
        }

        return false;
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
}
