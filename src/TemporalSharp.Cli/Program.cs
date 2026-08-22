using Microsoft.CodeAnalysis;
using TemporalSharp.Cli.Analysis;
using TemporalSharp.Cli.Reporting;

namespace TemporalSharp.Cli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Options options;
        try
        {
            options = Options.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Options.PrintUsage(Console.Error);
            return 2;
        }

        try
        {
            var solution = await ProjectLoader.LoadAsync(options.Path, CancellationToken.None).ConfigureAwait(false);
            var diagnostics = await AnalysisRunner.AnalyzeSolutionAsync(solution, CancellationToken.None, options.SeverityOverrides).ConfigureAwait(false);

            switch (options.Format)
            {
                case OutputFormat.Json:
                    Console.Out.WriteLine(Reporter.ToJson(diagnostics, options.SeverityOverrides));
                    break;

                case OutputFormat.Sarif:
                    Console.Out.WriteLine(Reporter.ToSarif(diagnostics, options.SeverityOverrides));
                    break;

                default:
                    Reporter.WriteConsole(Console.Out, diagnostics, options.SeverityOverrides);
                    break;
            }

            return ComputeExitCode(diagnostics, options.FailOn, options.SeverityOverrides);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 2;
        }
    }

    internal static int ComputeExitCode(
        IReadOnlyList<Diagnostic> diagnostics,
        DiagnosticSeverity? failOn,
        IReadOnlyDictionary<string, DiagnosticSeverity>? severityOverrides = null)
    {
        if (failOn is null)
        {
            return 0;
        }

        foreach (var diagnostic in diagnostics)
        {
            var severity = diagnostic.Severity;
            if (severityOverrides is not null && severityOverrides.TryGetValue(diagnostic.Id, out var overridden))
            {
                severity = overridden;
            }

            if (severity >= failOn.Value)
            {
                return 1;
            }
        }

        return 0;
    }
}
