using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TemporalSharp.Analyzers.Analyzers;

/// <summary>
/// Suppresses TemporalSharp diagnostics on lines (or the line immediately
/// following) a <c>//temporalsharp:ignore</c> comment.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TemporalSharpIgnoreSuppressor : DiagnosticSuppressor
{
    private const string IgnoreMarker = "temporalsharp:ignore";

    private static readonly string[] RuleIds =
    {
        "TMP0101", "TMP0102", "TMP0111", "TMP0121", "TMP0131", "TMP0141", "TMP0142", "TMP0151",
        "TMP1101", "TMP1102", "TMP1103", "TMP1104",
        "TMP2101", "TMP2102", "TMP2111", "TMP2121", "TMP2131", "TMP2141", "TMP2151", "TMP2161", "TMP2171",
        "TMP3101", "TMP3102", "TMP3103", "TMP3104", "TMP3201", "TMP3202", "TMP3301",
    };

    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions { get; } = BuildSuppressions();

    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        foreach (var diagnostic in context.ReportedDiagnostics)
        {
            var descriptor = FindDescriptor(diagnostic.Id);
            if (descriptor is null)
            {
                continue;
            }

            if (!IsIgnored(diagnostic))
            {
                continue;
            }

            context.ReportSuppression(Suppression.Create(descriptor, diagnostic));
        }
    }

    private static bool IsIgnored(Diagnostic diagnostic)
    {
        var tree = diagnostic.Location.SourceTree;
        if (tree is null)
        {
            return false;
        }

        var text = tree.GetText();
        var lineSpan = diagnostic.Location.GetLineSpan();
        if (!lineSpan.IsValid)
        {
            return false;
        }

        var line = lineSpan.StartLinePosition.Line;
        if (text.Lines[line].ToString().IndexOf(IgnoreMarker, StringComparison.Ordinal) >= 0)
        {
            return true;
        }

        if (line > 0)
        {
            var previous = text.Lines[line - 1].ToString().Trim();
            if (previous.StartsWith("//", StringComparison.Ordinal) &&
                previous.IndexOf(IgnoreMarker, StringComparison.Ordinal) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static SuppressionDescriptor? FindDescriptor(string diagnosticId)
    {
        foreach (var descriptor in BuildSuppressions())
        {
            if (descriptor.SuppressedDiagnosticId == diagnosticId)
            {
                return descriptor;
            }
        }

        return null;
    }

    private static ImmutableArray<SuppressionDescriptor> BuildSuppressions()
    {
        var builder = ImmutableArray.CreateBuilder<SuppressionDescriptor>(RuleIds.Length);
        foreach (var ruleId in RuleIds)
        {
            builder.Add(new SuppressionDescriptor(
                "TSIG" + ruleId,
                ruleId,
                "Suppressed by a //temporalsharp:ignore comment."));
        }

        return builder.MoveToImmutable();
    }
}
