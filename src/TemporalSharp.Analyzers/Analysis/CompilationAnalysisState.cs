using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TemporalSharp.Analyzers.Analysis;

/// <summary>
/// Per-compilation analysis state, computed once and shared across analyzers.
/// Contains the set of methods reachable from workflow code (the transitive
/// closure over the call graph, starting from methods of [Workflow] types).
/// </summary>
internal sealed class CompilationAnalysisState
{
    private static readonly ConditionalWeakTable<Compilation, CompilationAnalysisState> Cache = new();

    private readonly ImmutableHashSet<IMethodSymbol> _workflowReachable;

    private CompilationAnalysisState(ImmutableHashSet<IMethodSymbol> workflowReachable)
    {
        _workflowReachable = workflowReachable;
    }

    public static CompilationAnalysisState Get(Compilation compilation)
        => Cache.GetValue(compilation, static c => Create(c));

    /// <summary>
    /// Returns true if the given node is inside a method that is reachable from
    /// workflow code (including lambdas and local functions nested within such
    /// a method).
    /// </summary>
    public bool IsWorkflowReachable(SyntaxNode node, SemanticModel model)
    {
        var regularMethod = GetEnclosingRegularMethod(model.GetEnclosingSymbol(node.SpanStart));
        return regularMethod != null && _workflowReachable.Contains(regularMethod);
    }

    private static IMethodSymbol? GetEnclosingRegularMethod(ISymbol? symbol)
    {
        for (var current = symbol; current != null; current = current.ContainingSymbol)
        {
            if (current is IMethodSymbol method &&
                method.MethodKind is not (MethodKind.LambdaMethod or MethodKind.LocalFunction))
            {
                return method;
            }
        }

        return null;
    }

    private static CompilationAnalysisState Create(Compilation compilation)
    {
        var roots = new List<IMethodSymbol>();
        var edges = new Dictionary<IMethodSymbol, HashSet<IMethodSymbol>>(SymbolEqualityComparer.Default);

        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            foreach (var typeDeclaration in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                var typeSymbol = semanticModel.GetDeclaredSymbol(typeDeclaration);
                if (typeSymbol is null || !WorkflowDetection.IsWorkflowType(typeSymbol))
                {
                    continue;
                }

                foreach (var member in typeSymbol.GetMembers())
                {
                    if (member is IMethodSymbol method)
                    {
                        roots.Add(method);
                    }
                }
            }

            foreach (var node in root.DescendantNodes())
            {
                if (node is not (InvocationExpressionSyntax or ObjectCreationExpressionSyntax))
                {
                    continue;
                }

                var target = semanticModel.GetSymbolInfo(node).Symbol as IMethodSymbol;
                if (target is null || target.DeclaringSyntaxReferences.Length == 0)
                {
                    continue;
                }

                if (WorkflowDetection.IsActivityMethod(target))
                {
                    continue;
                }

                var caller = GetEnclosingRegularMethod(semanticModel.GetEnclosingSymbol(node.SpanStart));
                if (caller is null)
                {
                    continue;
                }

                if (!edges.TryGetValue(caller, out var callees))
                {
                    callees = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
                    edges[caller] = callees;
                }

                callees.Add(target);
            }
        }

        var reachable = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
        var queue = new Queue<IMethodSymbol>();
        foreach (var root in roots)
        {
            if (reachable.Add(root))
            {
                queue.Enqueue(root);
            }
        }

        while (queue.Count > 0)
        {
            var method = queue.Dequeue();
            if (!edges.TryGetValue(method, out var callees))
            {
                continue;
            }

            foreach (var callee in callees)
            {
                if (reachable.Add(callee))
                {
                    queue.Enqueue(callee);
                }
            }
        }

        return new CompilationAnalysisState(
            ImmutableHashSet.CreateRange<IMethodSymbol>(SymbolEqualityComparer.Default, reachable));
    }
}
