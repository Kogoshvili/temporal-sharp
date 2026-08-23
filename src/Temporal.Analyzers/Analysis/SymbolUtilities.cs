using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Kogoshvili.Temporal.Analyzers.Analysis;

/// <summary>
/// Small symbol-navigation helpers shared by the call-graph builder and the CLI.
/// </summary>
internal static class SymbolUtilities
{
    private static readonly ImmutableHashSet<string> MutatingMethodNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Add", "AddRange", "Clear", "Insert", "InsertRange",
        "Remove", "RemoveAll", "RemoveAt", "RemoveRange",
        "TryAdd", "TryRemove", "TryUpdate", "AddOrUpdate",
        "Enqueue", "Dequeue", "Push", "Pop", "Sort", "Reverse");

    /// <summary>
    /// Returns the nearest enclosing "regular" method (skipping lambdas and local
    /// functions), or null when the symbol is not inside a method.
    /// </summary>
    public static IMethodSymbol? GetEnclosingRegularMethod(ISymbol? symbol)
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

    /// <summary>
    /// Resolves the instance field/property mutated by <paramref name="target"/>,
    /// walking through nested member accesses (e.g. <c>_state.Nested.Foo</c>
    /// resolves to <c>_state</c>).
    /// </summary>
    public static bool TryGetMutatedInstanceMember(ExpressionSyntax target, SemanticModel model, out ISymbol member)
    {
        var symbol = ResolveRoot(target, model);
        return TryResolveInstanceMember(symbol, out member);
    }

    /// <summary>
    /// Detects collection-style mutation via a mutating method call on an
    /// instance member (e.g. <c>_items.Add(x)</c>, <c>this._queue.Enqueue(x)</c>).
    /// </summary>
    public static bool TryGetMutatedInstanceMember(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        out ISymbol member)
    {
        member = null!;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        if (!MutatingMethodNames.Contains(memberAccess.Name.Identifier.ValueText))
        {
            return false;
        }

        return TryGetMutatedInstanceMember(memberAccess.Expression, model, out member);
    }

    private static ISymbol? ResolveRoot(ExpressionSyntax target, SemanticModel model)
    {
        var current = target;
        while (current is MemberAccessExpressionSyntax memberAccess)
        {
            var receiver = memberAccess.Expression;
            if (receiver is ThisExpressionSyntax or BaseExpressionSyntax)
            {
                return model.GetSymbolInfo(memberAccess).Symbol;
            }

            current = receiver;
        }

        if (current is IdentifierNameSyntax or ThisExpressionSyntax or BaseExpressionSyntax)
        {
            return model.GetSymbolInfo(current).Symbol;
        }

        return null;
    }

    private static bool TryResolveInstanceMember(ISymbol? symbol, out ISymbol member)
    {
        member = symbol!;
        return symbol switch
        {
            IFieldSymbol { IsStatic: false } => true,
            IPropertySymbol { IsStatic: false, SetMethod: not null } => true,
            _ => false,
        };
    }
}
