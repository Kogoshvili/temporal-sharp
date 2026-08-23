using System.Text.RegularExpressions;

namespace Kogoshvili.Temporal.Analyzers.Analysis;

/// <summary>
/// Minimal path glob matching for the workflow auto-detection fallback. Supports
/// <c>*</c> (any characters except a path separator) and <c>**</c> (any
/// characters, including separators). Matching is case-insensitive and treats
/// both <c>/</c> and <c>\</c> as separators.
/// </summary>
internal static class PathGlob
{
    public static bool IsMatch(string glob, string path)
    {
        var normalizedGlob = glob.Replace('\\', '/');
        var normalizedPath = path.Replace('\\', '/');

        var pattern = "^" +
            Regex.Escape(normalizedGlob)
                .Replace(@"\*\*", ".*")
                .Replace(@"\*", @"[^/]*") +
            "$";

        return Regex.IsMatch(normalizedPath, pattern, RegexOptions.IgnoreCase);
    }
}
