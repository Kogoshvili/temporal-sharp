using Microsoft.CodeAnalysis;

namespace Kogoshvili.Temporal.Analyzers.Analysis;

/// <summary>
/// Produces a stable string key for a method, identical across different
/// <see cref="Compilation"/> instances (e.g. two projects in one solution), so
/// the solution-level call graph can link callers and callees that live in
/// different projects.
/// </summary>
internal static class ReachabilityKey
{
    public static string Method(IMethodSymbol method)
        => method.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
}
