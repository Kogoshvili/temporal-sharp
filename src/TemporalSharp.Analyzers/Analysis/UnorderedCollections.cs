using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TemporalSharp.Analyzers.Analysis;

/// <summary>
/// Determines whether a collection is enumerated in a non-deterministic order
/// (TMP0151). Dictionary/HashSet-like types are unordered; sorted collections and
/// OrderBy/OrderByDescending results are deterministic.
/// </summary>
internal static class UnorderedCollections
{
    private static readonly ImmutableHashSet<string> Unordered = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "System.Collections.Generic.Dictionary",
        "System.Collections.Generic.HashSet",
        "System.Collections.Hashtable",
        "System.Collections.Concurrent.ConcurrentDictionary",
        "System.Collections.Generic.ISet",
        "System.Collections.Generic.IDictionary",
        "System.Collections.Generic.IReadOnlyDictionary");

    private static readonly ImmutableHashSet<string> Sorted = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "System.Collections.Generic.SortedDictionary",
        "System.Collections.Generic.SortedSet",
        "System.Collections.Generic.SortedList");

    public static bool IsUnordered(ITypeSymbol? type) =>
        type is not null && Unordered.Contains(TypeNames.FullName(type));

    public static bool IsSorted(ITypeSymbol? type) =>
        type is not null && Sorted.Contains(TypeNames.FullName(type));

    /// <summary>
    /// True when the expression is (directly) a call to OrderBy/OrderByDescending,
    /// which yields a deterministic enumeration order.
    /// </summary>
    public static bool IsOrderBy(ExpressionSyntax expression)
    {
        if (expression is not InvocationExpressionSyntax invocation)
        {
            return false;
        }

        var name = invocation.Expression switch
        {
            MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
            _ => string.Empty,
        };

        return name is "OrderBy" or "OrderByDescending" or "Order" or "OrderDescending";
    }
}
