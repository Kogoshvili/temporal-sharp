using System.Text;
using Microsoft.CodeAnalysis;
using Kogoshvili.Temporal.Cli.Analysis;

namespace Kogoshvili.Temporal.Cli.Docs;

/// <summary>
/// Generates <c>RULES.md</c> from the analyzer's diagnostic descriptors, making
/// the descriptors the single source of truth for the rule catalog.
/// </summary>
internal static class RulesDocGenerator
{
    private static readonly (string Section, string Category, string? Prefix)[] Layout =
    {
        ("Determinism", "Determinism", null),
        ("Shared-state mutation", "WorkflowState", null),
        ("SDK feature-misuse", "SdkMisuse", "TMP2"),
        (".NET-specific", "SdkMisuse", "TMP3"),
        ("Best practice", "BestPractice", null),
        ("Testing", "Testing", null),
    };

    public static string Generate()
    {
        var descriptors = AnalysisRunner.Analyzers
            .SelectMany(a => a.SupportedDiagnostics)
            .GroupBy(d => d.Id, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(d => d.Id, StringComparer.Ordinal)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("# Kogoshvili.Temporal Rule Catalog");
        sb.AppendLine();
        sb.AppendLine("Rules implemented by the Kogoshvili.Temporal analyzer, grouped by category. The `Default`");
        sb.AppendLine("column is the default severity; `off` means the rule is opt-in — disabled by");
        sb.AppendLine("default and enabled via `.editorconfig` severity.");
        sb.AppendLine();
        sb.AppendLine("<!-- This file is generated from DiagnosticDescriptors.cs by `temporal-sharp docs`. Do not edit by hand. -->");
        sb.AppendLine();

        foreach (var (section, category, prefix) in Layout)
        {
            var rules = descriptors
                .Where(d => d.Category == category &&
                            (prefix is null || d.Id.StartsWith(prefix, StringComparison.Ordinal)))
                .ToList();

            if (rules.Count == 0)
            {
                continue;
            }

            sb.AppendLine($"## {section}");
            sb.AppendLine();

            sb.AppendLine("| ID | Default | Rule | Description |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var rule in rules)
            {
                sb.AppendLine($"| {rule.Id} | {Severity(rule)} | {Escape(rule.Title.ToString())} | {Escape(rule.Description.ToString())} |");
            }

            sb.AppendLine();
        }

        return sb.ToString().TrimEnd() + "\n";
    }

    private static string Severity(DiagnosticDescriptor descriptor) =>
        descriptor.IsEnabledByDefault ? descriptor.DefaultSeverity.ToString() : "off";

    private static string Escape(string text) =>
        text.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
}
