using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Kogoshvili.Temporal.Analyzers.Analysis;

namespace Kogoshvili.Temporal.Cli.Analysis;

/// <summary>
/// Builds a solution-wide call graph keyed by a stable method signature, then
/// computes the set of methods reachable from workflow code across all projects.
/// This lets the per-project analyzers flag a workflow that calls a helper in a
/// different project which, in turn, uses a deny-listed member.
/// </summary>
internal static class SolutionCallGraph
{
    public static async Task<ImmutableHashSet<string>> ComputeReachableAsync(
        Solution solution,
        CancellationToken cancellationToken)
    {
        var edges = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var roots = new HashSet<string>(StringComparer.Ordinal);

        foreach (var projectId in solution.ProjectIds)
        {
            var project = solution.GetProject(projectId);
            if (project is null)
            {
                continue;
            }

            var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
            if (compilation is null)
            {
                continue;
            }

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
                            roots.Add(ReachabilityKey.Method(method));
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
                    if (target is null || WorkflowDetection.IsActivityMethod(target))
                    {
                        continue;
                    }

                    var caller = SymbolUtilities.GetEnclosingRegularMethod(
                        semanticModel.GetEnclosingSymbol(node.SpanStart));
                    if (caller is null)
                    {
                        continue;
                    }

                    AddEdge(edges, ReachabilityKey.Method(caller), ReachabilityKey.Method(target));
                }
            }
        }

        var reachable = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
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

        return reachable.ToImmutableHashSet(StringComparer.Ordinal);
    }

    private static void AddEdge(Dictionary<string, HashSet<string>> edges, string caller, string callee)
    {
        if (!edges.TryGetValue(caller, out var callees))
        {
            callees = new HashSet<string>(StringComparer.Ordinal);
            edges[caller] = callees;
        }

        callees.Add(callee);
    }
}
