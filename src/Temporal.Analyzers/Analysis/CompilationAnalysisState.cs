using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kogoshvili.Temporal.Analyzers.Analysis;

/// <summary>
/// Per-compilation analysis state, computed once and shared across analyzers.
/// Contains the set of methods reachable from workflow code (the transitive
/// closure over the call graph, starting from methods of [Workflow] types).
/// Reachability is augmented by an optional solution-level key set supplied by
/// the CLI via an additional file, so cross-project chains are detected.
/// </summary>
internal sealed class CompilationAnalysisState
{
    public const string SolutionReachabilityFileName = "Kogoshvili.Temporal.SolutionReachability.txt";

    private const string WorkflowPathsKey = "kogoshvili.temporal.workflow_paths";

    private static readonly ConditionalWeakTable<Compilation, CompilationAnalysisState> Cache = new();

    private readonly ImmutableHashSet<IMethodSymbol> _workflowReachable;
    private readonly ImmutableHashSet<INamedTypeSymbol> _workflowTypes;
    private readonly ImmutableHashSet<string>? _solutionReachableKeys;

    private CompilationAnalysisState(
        ImmutableHashSet<IMethodSymbol> workflowReachable,
        ImmutableHashSet<INamedTypeSymbol> workflowTypes,
        ImmutableHashSet<string>? solutionReachableKeys)
    {
        _workflowReachable = workflowReachable;
        _workflowTypes = workflowTypes;
        _solutionReachableKeys = solutionReachableKeys;
    }

    public static CompilationAnalysisState Get(Compilation compilation, AnalyzerOptions options)
        => Cache.GetValue(
            compilation,
            c => Create(c, ReadSolutionReachability(options), options.AnalyzerConfigOptionsProvider));

    /// <summary>
    /// Reads the solution-level reachable-method keys emitted by the CLI, if any.
    /// </summary>
    public static ImmutableHashSet<string>? ReadSolutionReachability(AnalyzerOptions options)
    {
        var file = options.AdditionalFiles.FirstOrDefault(f =>
            f.Path.EndsWith(SolutionReachabilityFileName, StringComparison.Ordinal));
        if (file is null)
        {
            return null;
        }

        var text = file.GetText(CancellationToken.None);
        if (text is null)
        {
            return null;
        }

        return text.Lines
            .Select(l => l.ToString())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToImmutableHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Reads the opt-in path-convention globs used to treat files under e.g.
    /// <c>**/Workflows/**</c> as workflow code even when types are not annotated
    /// with <c>[Workflow]</c>.
    /// </summary>
    public static IReadOnlyList<string> ReadWorkflowPathGlobs(AnalyzerConfigOptions options)
    {
        if (!options.TryGetValue(WorkflowPathsKey, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return value
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(g => g.Trim())
            .Where(g => g.Length > 0)
            .ToList();
    }

    /// <summary>
    /// Returns true if the given node is inside a method that is reachable from
    /// workflow code (including lambdas and local functions nested within such
    /// a method).
    /// </summary>
    public bool IsWorkflowReachable(SyntaxNode node, SemanticModel model)
    {
        var regularMethod = SymbolUtilities.GetEnclosingRegularMethod(model.GetEnclosingSymbol(node.SpanStart));
        if (regularMethod is not null)
        {
            if (_workflowReachable.Contains(regularMethod))
            {
                return true;
            }

            return _solutionReachableKeys is not null &&
                   _solutionReachableKeys.Contains(ReachabilityKey.Method(regularMethod));
        }

        // Field/property initializers of a workflow type run during workflow
        // construction, so they are workflow code even though they are not inside
        // a method.
        return IsInWorkflowTypeInitializer(node, model);
    }

    private bool IsInWorkflowTypeInitializer(SyntaxNode node, SemanticModel model)
    {
        for (var symbol = model.GetEnclosingSymbol(node.SpanStart); symbol is not null; symbol = symbol.ContainingSymbol)
        {
            if (symbol is IFieldSymbol { ContainingType: { } fieldType })
            {
                return _workflowTypes.Contains(fieldType);
            }

            if (symbol is IPropertySymbol { ContainingType: { } propertyType })
            {
                return _workflowTypes.Contains(propertyType);
            }
        }

        return false;
    }

    private static CompilationAnalysisState Create(
        Compilation compilation,
        ImmutableHashSet<string>? solutionReachableKeys,
        AnalyzerConfigOptionsProvider analyzerConfigOptions)
    {
        var roots = new List<IMethodSymbol>();
        var workflowTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var edges = new Dictionary<IMethodSymbol, HashSet<IMethodSymbol>>(SymbolEqualityComparer.Default);
        var implementations = BuildOverrideMap(compilation);
        var delegateTargets = new Dictionary<ISymbol, List<IMethodSymbol>>(SymbolEqualityComparer.Default);

        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();
            var workflowGlobs = ReadWorkflowPathGlobs(analyzerConfigOptions.GetOptions(syntaxTree));
            var isWorkflowPath = workflowGlobs.Count > 0 &&
                                 workflowGlobs.Any(g => PathGlob.IsMatch(g, syntaxTree.FilePath));

            foreach (var typeDeclaration in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                var typeSymbol = semanticModel.GetDeclaredSymbol(typeDeclaration);
                if (typeSymbol is null ||
                    (!WorkflowDetection.IsWorkflowType(typeSymbol) && !isWorkflowPath))
                {
                    continue;
                }

                workflowTypes.Add(typeSymbol);

                foreach (var member in typeSymbol.GetMembers())
                {
                    if (member is IMethodSymbol method && !WorkflowDetection.IsActivityMethod(method))
                    {
                        roots.Add(method);
                    }
                }
            }

            CollectDelegateAssignments(root, semanticModel, delegateTargets);

            foreach (var node in root.DescendantNodes())
            {
                if (node is InvocationExpressionSyntax invocation)
                {
                    AddInvocationEdges(invocation, semanticModel, edges, implementations, delegateTargets);
                }
                else if (node is ObjectCreationExpressionSyntax creation)
                {
                    AddObjectCreationEdge(creation, semanticModel, edges);
                }
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
            ImmutableHashSet.CreateRange<IMethodSymbol>(SymbolEqualityComparer.Default, reachable),
            ImmutableHashSet.CreateRange<INamedTypeSymbol>(SymbolEqualityComparer.Default, workflowTypes),
            solutionReachableKeys);
    }

    private static void AddInvocationEdges(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        Dictionary<IMethodSymbol, HashSet<IMethodSymbol>> edges,
        Dictionary<IMethodSymbol, List<IMethodSymbol>> implementations,
        Dictionary<ISymbol, List<IMethodSymbol>> delegateTargets)
    {
        var caller = SymbolUtilities.GetEnclosingRegularMethod(semanticModel.GetEnclosingSymbol(invocation.SpanStart));
        if (caller is null)
        {
            return;
        }

        var target = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
        if (target is null || WorkflowDetection.IsActivityMethod(target))
        {
            return;
        }

        // Delegate invocation: resolve the receiver slot to its assigned targets.
        if (target.MethodKind == MethodKind.DelegateInvoke)
        {
            var receiver = (invocation.Expression as MemberAccessExpressionSyntax)?.Expression ?? invocation.Expression;
            var receiverSymbol = semanticModel.GetSymbolInfo(receiver).Symbol;
            if (receiverSymbol is not null && delegateTargets.TryGetValue(receiverSymbol, out var targets))
            {
                foreach (var t in targets)
                {
                    AddEdge(edges, caller, t);
                }
            }

            return;
        }

        AddEdge(edges, caller, target);

        // Virtual/interface dispatch: also reach every override/implementation.
        if (IsDispatchCandidate(target) && implementations.TryGetValue(target, out var impls))
        {
            foreach (var impl in impls)
            {
                AddEdge(edges, caller, impl);
            }
        }
    }

    private static void AddObjectCreationEdge(
        ObjectCreationExpressionSyntax creation,
        SemanticModel semanticModel,
        Dictionary<IMethodSymbol, HashSet<IMethodSymbol>> edges)
    {
        var caller = SymbolUtilities.GetEnclosingRegularMethod(semanticModel.GetEnclosingSymbol(creation.SpanStart));
        if (caller is null)
        {
            return;
        }

        var target = semanticModel.GetSymbolInfo(creation).Symbol as IMethodSymbol;
        if (target is null || WorkflowDetection.IsActivityMethod(target))
        {
            return;
        }

        AddEdge(edges, caller, target);
    }

    private static void AddEdge(
        Dictionary<IMethodSymbol, HashSet<IMethodSymbol>> edges,
        IMethodSymbol caller,
        IMethodSymbol callee)
    {
        if (!edges.TryGetValue(caller, out var callees))
        {
            callees = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
            edges[caller] = callees;
        }

        callees.Add(callee);
    }

    private static bool IsDispatchCandidate(IMethodSymbol method) =>
        method.IsVirtual || method.IsAbstract || method.ContainingType?.TypeKind == TypeKind.Interface;

    private static Dictionary<IMethodSymbol, List<IMethodSymbol>> BuildOverrideMap(Compilation compilation)
    {
        var methods = new List<IMethodSymbol>();
        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            foreach (var typeDeclaration in syntaxTree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                var typeSymbol = semanticModel.GetDeclaredSymbol(typeDeclaration);
                if (typeSymbol is null)
                {
                    continue;
                }

                foreach (var member in typeSymbol.GetMembers())
                {
                    if (member is IMethodSymbol method && method.DeclaringSyntaxReferences.Length > 0)
                    {
                        methods.Add(method);
                    }
                }
            }
        }

        var map = new Dictionary<IMethodSymbol, List<IMethodSymbol>>(SymbolEqualityComparer.Default);
        foreach (var method in methods)
        {
            for (var overridden = method.OverriddenMethod; overridden is not null; overridden = overridden.OverriddenMethod)
            {
                AddImplementation(map, overridden, method);
            }

            foreach (var ifaceMethod in method.ExplicitInterfaceImplementations)
            {
                AddImplementation(map, ifaceMethod, method);
            }

            foreach (var iface in method.ContainingType.AllInterfaces)
            {
                foreach (var ifaceMember in iface.GetMembers().OfType<IMethodSymbol>())
                {
                    var implementation = method.ContainingType.FindImplementationForInterfaceMember(ifaceMember);
                    if (implementation is not null &&
                        SymbolEqualityComparer.Default.Equals(implementation, method))
                    {
                        AddImplementation(map, ifaceMember, method);
                    }
                }
            }
        }

        return map;
    }

    private static void AddImplementation(
        Dictionary<IMethodSymbol, List<IMethodSymbol>> map,
        IMethodSymbol key,
        IMethodSymbol implementation)
    {
        if (!map.TryGetValue(key, out var list))
        {
            list = new List<IMethodSymbol>();
            map[key] = list;
        }

        if (!list.Contains(implementation, SymbolEqualityComparer.Default))
        {
            list.Add(implementation);
        }
    }

    private static void CollectDelegateAssignments(
        SyntaxNode root,
        SemanticModel semanticModel,
        Dictionary<ISymbol, List<IMethodSymbol>> delegateTargets)
    {
        foreach (var node in root.DescendantNodes())
        {
            if (node is AssignmentExpressionSyntax assignment)
            {
                var slot = semanticModel.GetSymbolInfo(assignment.Left).Symbol;
                if (slot is null || !IsDelegateSlot(slot))
                {
                    continue;
                }

                var target = semanticModel.GetSymbolInfo(assignment.Right).Symbol as IMethodSymbol;
                if (target is not null)
                {
                    AddDelegateTarget(delegateTargets, slot, target);
                }
            }
            else if (node is VariableDeclaratorSyntax { Initializer.Value: { } initializer })
            {
                var slot = semanticModel.GetDeclaredSymbol(node);
                if (slot is null || !IsDelegateSlot(slot))
                {
                    continue;
                }

                var target = semanticModel.GetSymbolInfo(initializer).Symbol as IMethodSymbol;
                if (target is not null)
                {
                    AddDelegateTarget(delegateTargets, slot, target);
                }
            }
        }
    }

    private static bool IsDelegateSlot(ISymbol symbol) => symbol switch
    {
        ILocalSymbol local => local.Type.TypeKind == TypeKind.Delegate,
        IFieldSymbol field => field.Type.TypeKind == TypeKind.Delegate,
        IParameterSymbol parameter => parameter.Type.TypeKind == TypeKind.Delegate,
        IPropertySymbol property => property.Type.TypeKind == TypeKind.Delegate,
        _ => false,
    };

    private static void AddDelegateTarget(
        Dictionary<ISymbol, List<IMethodSymbol>> delegateTargets,
        ISymbol slot,
        IMethodSymbol target)
    {
        if (!delegateTargets.TryGetValue(slot, out var targets))
        {
            targets = new List<IMethodSymbol>();
            delegateTargets[slot] = targets;
        }

        if (!targets.Contains(target, SymbolEqualityComparer.Default))
        {
            targets.Add(target);
        }
    }
}
