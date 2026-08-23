using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Kogoshvili.Temporal.Analyzers.Analysis;

/// <summary>
/// Computes, once per compilation, the set of activity methods invoked via
/// <c>Workflow.ExecuteLocalActivityAsync</c>, so local activities can be
/// distinguished from regular activities. Kept outside <see cref="DiagnosticAnalyzer"/>
/// subclasses because it needs to resolve symbols across syntax trees.
/// </summary>
internal sealed class LocalActivityIndex
{
    private static readonly ConditionalWeakTable<Compilation, LocalActivityIndex> Cache = new();

    private readonly ImmutableHashSet<IMethodSymbol> _localActivities;
    private readonly ImmutableHashSet<string> _localActivityNames;

    private LocalActivityIndex(
        ImmutableHashSet<IMethodSymbol> localActivities,
        ImmutableHashSet<string> localActivityNames)
    {
        _localActivities = localActivities;
        _localActivityNames = localActivityNames;
    }

    public static LocalActivityIndex Get(Compilation compilation)
        => Cache.GetValue(compilation, c => Create(c));

    public bool IsLocalActivity(IMethodSymbol method) =>
        _localActivities.Contains(method) ||
        (WorkflowDetection.IsActivityMethod(method) && _localActivityNames.Contains(method.Name));

    private static LocalActivityIndex Create(Compilation compilation)
    {
        var activities = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var node in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (model.GetSymbolInfo(node).Symbol is not IMethodSymbol method ||
                    method.Name != "ExecuteLocalActivityAsync" ||
                    method.ContainingType is null ||
                    !SdkNames.IsWorkflowType(method.ContainingType))
                {
                    continue;
                }

                var target = LambdaTargetResolver.ResolveTypedLambdaTarget(model, node);
                if (target is not null)
                {
                    activities.Add(target);
                    names.Add(target.Name);
                    continue;
                }

                var first = node.ArgumentList.Arguments.FirstOrDefault();
                if (first?.Expression is LiteralExpressionSyntax literal &&
                    literal.IsKind(SyntaxKind.StringLiteralExpression) &&
                    literal.Token.ValueText is string name)
                {
                    names.Add(name);
                }
            }
        }

        return new LocalActivityIndex(
            ImmutableHashSet.CreateRange<IMethodSymbol>(SymbolEqualityComparer.Default, activities),
            ImmutableHashSet.CreateRange<string>(StringComparer.Ordinal, names));
    }
}
