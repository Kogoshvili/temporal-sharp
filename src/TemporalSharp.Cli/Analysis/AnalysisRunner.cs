using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using TemporalSharp.Analyzers.Analyzers;

namespace TemporalSharp.Cli.Analysis;

internal static class AnalysisRunner
{
    private static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(
        new DeterminismAnalyzer(),
        new WorkflowStateAnalyzer(),
        new SdkMisuseAnalyzer());

    public static async Task<ImmutableArray<Diagnostic>> AnalyzeSolutionAsync(Solution solution, CancellationToken cancellationToken)
    {
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

            results.AddRange(await AnalyzeCompilationAsync(compilation, project.AnalyzerOptions, cancellationToken).ConfigureAwait(false));
        }

        return results.ToImmutable();
    }

    public static async Task<ImmutableArray<Diagnostic>> AnalyzeCompilationAsync(
        Compilation compilation,
        AnalyzerOptions? options,
        CancellationToken cancellationToken)
    {
        var withAnalyzers = compilation.WithAnalyzers(Analyzers, options);
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync(cancellationToken).ConfigureAwait(false);
    }
}
