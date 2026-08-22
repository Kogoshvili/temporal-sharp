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
            DiagnosticDescriptors.StopwatchUsage,
            DiagnosticDescriptors.IoOrEnvironmentAccess,
            DiagnosticDescriptors.ConcurrentExecution,
            DiagnosticDescriptors.BlockingPrimitive,
            DiagnosticDescriptors.TaskScheduling,
            DiagnosticDescriptors.UnorderedEnumeration);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => Supported;

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
        {
            var state = CompilationAnalysisState.Get(startContext.Compilation, startContext.Options);

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, state),
                SyntaxKind.InvocationExpression);

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeObjectCreation(nodeContext, state),
                SyntaxKind.ObjectCreationExpression);

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeMemberAccess(nodeContext, state),
                SyntaxKind.SimpleMemberAccessExpression);

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeForEach(nodeContext, state),
                SyntaxKind.ForEachStatement);

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeLock(nodeContext, state),
                SyntaxKind.LockStatement);
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

        var key = SymbolKeys.Member(symbol);

        // Concurrency constructors (e.g. new Thread(...), new BackgroundWorker())
        // are flagged regardless of argument count.
        if (DenyList.TryGetConcurrencyConstructor(key, out var concurrencyDescriptor))
        {
            ReportIfReachable(context, state, node, symbol, concurrencyDescriptor);
            return;
        }

        // Only parameterless constructors of non-deterministic types are flagged
        // (e.g. new Random()); a seeded constructor is deterministic.
        if (node.ArgumentList is null || node.ArgumentList.Arguments.Count != 0)
        {
            return;
        }

        if (!DenyList.TryGetConstructor(key, out var descriptor))
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

    private static void AnalyzeForEach(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var node = (ForEachStatementSyntax)context.Node;
        var collectionType = context.SemanticModel.GetTypeInfo(node.Expression).Type;
        if (collectionType is null)
        {
            return;
        }

        if (!state.IsWorkflowReachable(node, context.SemanticModel))
        {
            return;
        }

        if (UnorderedCollections.IsSorted(collectionType) || UnorderedCollections.IsOrderBy(node.Expression))
        {
            return;
        }

        if (!UnorderedCollections.IsUnordered(collectionType))
        {
            return;
        }

        var display = collectionType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.UnorderedEnumeration, node.ForEachKeyword.GetLocation(), display));
    }

    private static void AnalyzeLock(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var node = (LockStatementSyntax)context.Node;
        if (!state.IsWorkflowReachable(node, context.SemanticModel))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.BlockingPrimitive, node.LockKeyword.GetLocation(), "lock"));
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
