using Microsoft.CodeAnalysis;

namespace TemporalSharp.Analyzers.Analysis;

/// <summary>
/// Robust type-name helpers for matching BCL generic types, independent of type
/// parameter names. <see cref="INamedTypeSymbol.Name"/> omits generic arity and
/// type arguments, so "Dictionary&lt;TKey,TValue&gt;" matches as "Dictionary".
/// </summary>
internal static class TypeNames
{
    public static string FullName(ITypeSymbol type)
    {
        var ns = type.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        var name = type.OriginalDefinition?.Name ?? type.Name;
        return string.IsNullOrEmpty(ns) ? name : ns + "." + name;
    }

    /// <summary>True if the type is <paramref name="fullName"/> or derives from it.</summary>
    public static bool IsOrDerivesFrom(ITypeSymbol type, string fullName)
    {
        for (ITypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (FullName(current) == fullName)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True if the type is <paramref name="fullName"/> or implements/inherits it.</summary>
    public static bool IsOrImplements(ITypeSymbol type, string fullName)
    {
        if (IsOrDerivesFrom(type, fullName))
        {
            return true;
        }

        if (type.TypeKind == TypeKind.Interface && FullName(type) == fullName)
        {
            return true;
        }

        foreach (var iface in type.AllInterfaces)
        {
            if (FullName(iface) == fullName)
            {
                return true;
            }
        }

        return false;
    }
}
