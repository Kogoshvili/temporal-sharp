using Microsoft.CodeAnalysis;

namespace TemporalSharp.Analyzers.Analysis;

/// <summary>
/// Canonical string keys for matching symbols against deny-lists. Keys are
/// "ContainingType.MemberName" using the C# error-message display format.
/// </summary>
internal static class SymbolKeys
{
    public static string Member(ISymbol symbol)
    {
        var typeName = symbol.ContainingType?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) ?? string.Empty;
        return typeName + "." + symbol.Name;
    }
}
