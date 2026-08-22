using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TemporalSharp.Analyzers.Analysis;

/// <summary>
/// Reads TemporalSharp configuration from .editorconfig via the analyzer config
/// options provider.
/// </summary>
internal sealed class TemporalSharpConfig
{
    private const string DefaultSensitivePattern =
        @"(?i)(password|passwd|secret|token|apikey|api_key|credential|connectionstring)";

    private readonly AnalyzerConfigOptionsProvider _provider;

    private TemporalSharpConfig(AnalyzerConfigOptionsProvider provider) => _provider = provider;

    public static TemporalSharpConfig From(AnalyzerConfigOptionsProvider provider) => new(provider);

    /// <summary>Gets the sensitive-data regex, honoring the custom key or the default.</summary>
    public string SensitivePattern(SyntaxTree? tree)
    {
        var options = tree is not null ? _provider.GetOptions(tree) : _provider.GlobalOptions;
        if (options.TryGetValue("temporalsharp.sensitive_pattern", out var pattern) &&
            !string.IsNullOrWhiteSpace(pattern))
        {
            return pattern;
        }

        return DefaultSensitivePattern;
    }
}
