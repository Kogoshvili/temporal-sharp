using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Kogoshvili.Temporal.Analyzers.Analysis;
using Kogoshvili.Temporal.Analyzers.Diagnostics;

namespace Kogoshvili.Temporal.Analyzers.Analyzers;

/// <summary>
/// Flags activity execution-context misuse: logging to a non-SDK logger
/// (TMP3106) and HTTP calls without a <c>CancellationToken</c> (TMP3107).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ActivityContextAnalyzer : DiagnosticAnalyzer
{
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
            DiagnosticDescriptors.NonSdkActivityLog,
            DiagnosticDescriptors.HttpClientWithoutCancellation);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
        {
            return;
        }

        if (GetEnclosingActivityMethod(context, invocation) is not { })
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
