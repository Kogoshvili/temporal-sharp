using Microsoft.CodeAnalysis;

namespace TemporalSharp.Analyzers.Analysis;

/// <summary>
/// Small symbol-navigation helpers shared by the call-graph builder and the CLI.
/// </summary>
internal static class SymbolUtilities
{
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
}
