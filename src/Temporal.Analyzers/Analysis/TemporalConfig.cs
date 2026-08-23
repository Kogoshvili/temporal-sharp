using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kogoshvili.Temporal.Analyzers.Analysis;

/// <summary>
/// Reads Kogoshvili.Temporal configuration from .editorconfig via the analyzer config
/// options provider.
/// </summary>
internal sealed class TemporalConfig
{
    private const string DefaultSensitivePattern =
        @"(?i)(password|passwd|secret|token|apikey|api_key|credential|connectionstring)";

    private static readonly IReadOnlyDictionary<string, string> EmptyAttributes =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private readonly AnalyzerConfigOptionsProvider _provider;

    private TemporalConfig(AnalyzerConfigOptionsProvider provider) => _provider = provider;

    public static TemporalConfig From(AnalyzerConfigOptionsProvider provider) => new(provider);

    /// <summary>Gets the sensitive-data regex, honoring the custom key or the default.</summary>
    public string SensitivePattern(SyntaxTree? tree)
    {
        var options = tree is not null ? _provider.GetOptions(tree) : _provider.GlobalOptions;
        if (options.TryGetValue("kogoshvili.temporal.sensitive_pattern", out var pattern) &&
            !string.IsNullOrWhiteSpace(pattern))
        {
            return pattern;
        }

        return DefaultSensitivePattern;
    }

    /// <summary>
    /// Parses the <c>kogoshvili.temporal.search_attributes</c> key into an
    /// alias→attribute map. Aliases are normalized (case/underscore/hyphen
    /// insensitive); the mapped attribute name is preserved verbatim.
    /// </summary>
    public IReadOnlyDictionary<string, string> SearchAttributes(SyntaxTree? tree)
    {
        var options = tree is not null ? _provider.GetOptions(tree) : _provider.GlobalOptions;
        if (!options.TryGetValue("kogoshvili.temporal.search_attributes", out var value) ||
            string.IsNullOrWhiteSpace(value))
        {
            return EmptyAttributes;
        }

        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in value.Split(','))
        {
            var parts = entry.Split('=');
            if (parts.Length != 2)
            {
                continue;
            }

            var alias = NormalizeName(parts[0]);
            var attribute = parts[1].Trim();
            if (alias.Length > 0 && attribute.Length > 0)
            {
                map[alias] = attribute;
            }
        }

        return map;
    }

    /// <summary>Normalizes an identifier for case/separator-insensitive matching.</summary>
    public static string NormalizeName(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString();
    }
}
