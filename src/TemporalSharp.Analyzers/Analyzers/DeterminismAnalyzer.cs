using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using TemporalSharp.Analyzers.Analysis;
using TemporalSharp.Analyzers.Diagnostics;

namespace TemporalSharp.Analyzers.Analyzers;

/// <summary>
/// Flags non-deterministic member access (wall-clock time, sleep/block,
/// randomness, I/O) in code reachable from workflow code.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DeterminismAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableArray<DiagnosticDescriptor> Supported =
        ImmutableArray.Create(
            DiagnosticDescriptors.WallClockTime,
            DiagnosticDescriptors.BlockOrSleep,
            DiagnosticDescriptors.NonDeterministicRandomness,
            DiagnosticDescriptors.IoOrEnvironmentAccess);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => Supported;

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
        {
            var state = CompilationAnalysisState.Get(startContext.Compilation);

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, state),
                SyntaxKind.InvocationExpression);

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeObjectCreation(nodeContext, state),
                SyntaxKind.ObjectCreationExpression);

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeMemberAccess(nodeContext, state),
                SyntaxKind.SimpleMemberAccessExpression);
        });
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var node = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(node).Symbol is not IMethodSymbol symbol)
        {
            return;
        }

        if (!DenyList.TryGetMember(SymbolKeys.Member(symbol), out var descriptor))
        {
            return;
        }

        ReportIfReachable(context, state, node, symbol, descriptor);
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var node = (ObjectCreationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(node).Symbol is not IMethodSymbol symbol)
        {
            return;
        }

        // Only parameterless constructors of non-deterministic types are flagged
        // (e.g. new Random()); a seeded constructor is deterministic.
        if (node.ArgumentList is null || node.ArgumentList.Arguments.Count != 0)
        {
            return;
        }

        if (!DenyList.TryGetConstructor(SymbolKeys.Member(symbol), out var descriptor))
        {
            return;
        }

        ReportIfReachable(context, state, node, symbol, descriptor);
    }

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var node = (MemberAccessExpressionSyntax)context.Node;
        var symbol = context.SemanticModel.GetSymbolInfo(node).Symbol;

        // Only property/field reads; method groups are handled by invocation
        // analysis and must not be double-reported.
        if (symbol is not (IPropertySymbol or IFieldSymbol))
        {
            return;
        }

        if (!DenyList.TryGetMember(SymbolKeys.Member(symbol), out var descriptor))
        {
            return;
        }

        ReportIfReachable(context, state, node, symbol, descriptor);
    }

    private static void ReportIfReachable(
        SyntaxNodeAnalysisContext context,
        CompilationAnalysisState state,
        SyntaxNode node,
        ISymbol symbol,
        DiagnosticDescriptor? descriptor)
    {
        if (descriptor is null)
        {
            return;
        }

        if (!state.IsWorkflowReachable(node, context.SemanticModel))
        {
            return;
        }

        var display = symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        context.ReportDiagnostic(Diagnostic.Create(descriptor, node.GetLocation(), display));
    }
}
