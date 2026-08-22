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

    private static readonly ImmutableHashSet<string> OrderExposingLinqMethods = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "ToList", "ToArray",
        "First", "FirstOrDefault", "Last", "LastOrDefault",
        "Single", "SingleOrDefault",
        "ElementAt", "ElementAtOrDefault",
        "Take", "TakeWhile", "Skip", "SkipWhile");

    private static readonly ImmutableHashSet<string> TransparentLinqMethods = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Where", "Select", "SelectMany", "OfType", "Cast", "AsEnumerable",
        "Distinct", "DefaultIfEmpty", "Append", "Prepend", "Concat");

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
                nodeContext => AnalyzeUnorderedMaterialization(nodeContext, state),
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
        if (!state.IsWorkflowReachable(node, context.SemanticModel))
        {
            return;
        }

        if (!TryGetUnorderedSource(node.Expression, context.SemanticModel, out var collectionType))
        {
            return;
        }

        var display = collectionType!.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.UnorderedEnumeration, node.ForEachKeyword.GetLocation(), display));
    }

    private static void AnalyzeUnorderedMaterialization(SyntaxNodeAnalysisContext context, CompilationAnalysisState state)
    {
        var node = (InvocationExpressionSyntax)context.Node;
        if (!state.IsWorkflowReachable(node, context.SemanticModel))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(node).Symbol is not IMethodSymbol method ||
            !IsLinqMethod(method) ||
            !OrderExposingLinqMethods.Contains(method.Name))
        {
            return;
        }

        var source = SourceExpression(node, method);
        if (source is null || !TryGetUnorderedSource(source, context.SemanticModel, out var type))
        {
            return;
        }

        var display = type!.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.UnorderedEnumeration, node.GetLocation(), display));
    }

    private static bool IsLinqMethod(IMethodSymbol method) =>
        method.ContainingType is { } containingType &&
        (TypeNames.FullName(containingType) == "System.Linq.Enumerable" ||
         TypeNames.FullName(containingType) == "System.Linq.Queryable");

    private static ExpressionSyntax? SourceExpression(InvocationExpressionSyntax invocation, IMethodSymbol method)
    {
        if (method.MethodKind == MethodKind.ReducedExtension)
        {
            return invocation.Expression is MemberAccessExpressionSyntax memberAccess
                ? memberAccess.Expression
                : null;
        }

        return invocation.ArgumentList?.Arguments.FirstOrDefault()?.Expression;
    }

    /// <summary>
    /// Determines whether <paramref name="expression"/> enumerates an unordered
    /// collection. Walks through order-preserving LINQ operators and the
    /// Dictionary.Keys/Values views; OrderBy/OrderByDescending and sorted
    /// collection types terminate the walk as deterministic.
    /// </summary>
    private static bool TryGetUnorderedSource(ExpressionSyntax expression, SemanticModel model, out ITypeSymbol? type)
    {
        if (UnorderedCollections.IsOrderBy(expression))
        {
            type = null;
            return false;
        }

        if (expression is InvocationExpressionSyntax invocation &&
            model.GetSymbolInfo(invocation).Symbol is IMethodSymbol method &&
            IsLinqMethod(method) &&
            TransparentLinqMethods.Contains(method.Name))
        {
            var inner = SourceExpression(invocation, method);
            if (inner is not null)
            {
                return TryGetUnorderedSource(inner, model, out type);
            }
        }

        if (expression is MemberAccessExpressionSyntax member &&
            member.Name.Identifier.ValueText is "Keys" or "Values")
        {
            var receiverType = model.GetTypeInfo(member.Expression).Type;
            if (receiverType is not null && UnorderedCollections.IsUnordered(receiverType))
            {
                type = model.GetTypeInfo(expression).Type;
                return type is not null;
            }
        }

        var collectionType = model.GetTypeInfo(expression).Type;
        if (collectionType is not null && UnorderedCollections.IsUnordered(collectionType))
        {
            type = collectionType;
            return true;
        }

        type = null;
        return false;
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
