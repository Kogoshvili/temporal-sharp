using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using TemporalSharp.Analyzers.Analysis;
using TemporalSharp.Analyzers.Analyzers;

namespace TemporalSharp.Cli.Analysis;

internal static class AnalysisRunner
{
    private static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(
        new DeterminismAnalyzer(),
        new WorkflowStateAnalyzer(),
        new SdkMisuseAnalyzer(),
        new ActivityHeartbeatAnalyzer(),
        new WorkflowContractAnalyzer(),
        new VersioningAnalyzer(),
        new SearchAttributeAnalyzer(),
        new TemporalSharpIgnoreSuppressor());

    public static async Task<ImmutableArray<Diagnostic>> AnalyzeSolutionAsync(Solution solution, CancellationToken cancellationToken)
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

            results.AddRange(await AnalyzeCompilationAsync(compilation, augmentedOptions, cancellationToken).ConfigureAwait(false));
        }

        return results.ToImmutable();
    }

    public static async Task<ImmutableArray<Diagnostic>> AnalyzeCompilationAsync(
        Compilation compilation,
        AnalyzerOptions? options,
        CancellationToken cancellationToken)
    {
        var withAnalyzers = compilation.WithAnalyzers(Analyzers, options);

        // GetAllDiagnosticsAsync applies the //temporalsharp:ignore suppressor;
        // keep only TemporalSharp rules and drop compiler diagnostics.
        var all = await withAnalyzers.GetAllDiagnosticsAsync(cancellationToken).ConfigureAwait(false);
        return all.Where(d => d.Id.StartsWith("TMP", StringComparison.Ordinal)).ToImmutableArray();
    }
}
