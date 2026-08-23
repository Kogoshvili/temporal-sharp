using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Kogoshvili.Temporal.Analyzers.Analysis;
using Kogoshvili.Temporal.Analyzers.Analyzers;

namespace Kogoshvili.Temporal.Cli.Analysis;

internal static class AnalysisRunner
{
    internal static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(
        new DeterminismAnalyzer(),
        new WorkflowStateAnalyzer(),
        new SdkMisuseAnalyzer(),
        new ActivityHeartbeatAnalyzer(),
        new WorkflowContractAnalyzer(),
        new ActivityStateAnalyzer(),
        new VersioningAnalyzer(),
        new SearchAttributeAnalyzer(),
        new WorkflowMessageAnalyzer(),
        new WorkflowUpdateAnalyzer(),
        new WorkflowLifecycleAnalyzer(),
        new ErrorHandlingAnalyzer(),
        new ActivityContextAnalyzer(),
        new SdkBoundaryAnalyzer());

    private static readonly ImmutableArray<string> RuleIds = Analyzers
        .SelectMany(a => a.SupportedDiagnostics)
        .Select(d => d.Id)
        .Distinct(StringComparer.Ordinal)
        .ToImmutableArray();

    public static async Task<ImmutableArray<Diagnostic>> AnalyzeSolutionAsync(
        Solution solution,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, DiagnosticSeverity>? severityOverrides = null)
    {
        var reachable = await SolutionCallGraph.ComputeReachableAsync(solution, cancellationToken).ConfigureAwait(false);
        var reachabilityFile = new InMemoryAdditionalText(
            CompilationAnalysisState.SolutionReachabilityFileName,
            string.Join("\n", reachable));

        var results = ImmutableArray.CreateBuilder<Diagnostic>();

        foreach (var projectId in solution.ProjectIds)
        {
            var project = solution.GetProject(projectId);
            if (project is null)
            {
                continue;
            }

            var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
            if (compilation is null)
            {
                continue;
            }

            var options = project.AnalyzerOptions;
            var augmentedOptions = new AnalyzerOptions(
                options.AdditionalFiles.Add(reachabilityFile),
                options.AnalyzerConfigOptionsProvider);

            results.AddRange(await AnalyzeCompilationAsync(compilation, augmentedOptions, cancellationToken, severityOverrides).ConfigureAwait(false));
        }

        return results.ToImmutable();
    }

    public static async Task<ImmutableArray<Diagnostic>> AnalyzeCompilationAsync(
        Compilation compilation,
        AnalyzerOptions? options,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, DiagnosticSeverity>? severityOverrides = null)
    {
        var options2 = compilation.Options.WithSpecificDiagnosticOptions(
            BuildSpecificDiagnosticOptions(compilation, options, severityOverrides));
        var withAnalyzers = compilation.WithOptions(options2).WithAnalyzers(Analyzers, options);

        // GetAllDiagnosticsAsync applies #pragma/SuppressMessage suppressions and
        // the severity overrides above; keep only Kogoshvili.Temporal rules and drop
        // compiler diagnostics.
        var all = await withAnalyzers.GetAllDiagnosticsAsync(cancellationToken).ConfigureAwait(false);
        return all.Where(d => d.Id.StartsWith("TMP", StringComparison.Ordinal)).ToImmutableArray();
    }

    private static ImmutableDictionary<string, ReportDiagnostic> BuildSpecificDiagnosticOptions(
        Compilation compilation,
        AnalyzerOptions? options,
        IReadOnlyDictionary<string, DiagnosticSeverity>? severityOverrides)
    {
        var builder = compilation.Options.SpecificDiagnosticOptions.ToBuilder();

        if (options?.AnalyzerConfigOptionsProvider is { } provider)
        {
            // Collect the most specific .editorconfig severity for each rule. Tree
            // options (e.g. [*.cs] sections) win over global options; we read each
            // tree's options only once.
            var seen = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var ruleId in RuleIds)
            {
                var key = $"dotnet_diagnostic.{ruleId}.severity";
                if (provider.GlobalOptions.TryGetValue(key, out var global))
                {
                    seen[ruleId] = global;
                }
            }

            foreach (var tree in compilation.SyntaxTrees)
            {
                var treeOptions = provider.GetOptions(tree);
                foreach (var ruleId in RuleIds)
                {
                    var key = $"dotnet_diagnostic.{ruleId}.severity";
                    if (treeOptions.TryGetValue(key, out var value))
                    {
                        seen[ruleId] = value;
                    }
                }
            }

            foreach (var pair in seen)
            {
                var report = ToReportDiagnostic(pair.Value);
                if (report == ReportDiagnostic.Default)
                {
                    builder.Remove(pair.Key);
                }
                else
                {
                    builder[pair.Key] = report;
                }
            }
        }

        if (severityOverrides is not null)
        {
            foreach (var pair in severityOverrides)
            {
                builder[pair.Key] = ToReportDiagnostic(pair.Value);
            }
        }

        return builder.ToImmutable();
    }

    private static ReportDiagnostic ToReportDiagnostic(string severity) => severity switch
    {
        "none" => ReportDiagnostic.Suppress,
        "silent" => ReportDiagnostic.Suppress,
        "suggestion" => ReportDiagnostic.Info,
        "info" => ReportDiagnostic.Info,
        "warning" => ReportDiagnostic.Warn,
        "error" => ReportDiagnostic.Error,
        _ => ReportDiagnostic.Default,
    };

    private static ReportDiagnostic ToReportDiagnostic(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Info => ReportDiagnostic.Info,
        DiagnosticSeverity.Warning => ReportDiagnostic.Warn,
        DiagnosticSeverity.Error => ReportDiagnostic.Error,
        _ => ReportDiagnostic.Default,
    };
}
