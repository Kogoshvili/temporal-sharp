using Microsoft.CodeAnalysis;
using Kogoshvili.Temporal.Cli.Analysis;
using Kogoshvili.Temporal.Cli.Docs;
using Kogoshvili.Temporal.Cli.Map;
using Kogoshvili.Temporal.Cli.Presets;
using Kogoshvili.Temporal.Cli.Reporting;

namespace Kogoshvili.Temporal.Cli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "map")
        {
            return await MapCommand.RunAsync(args).ConfigureAwait(false);
        }

        if (args.Length > 0 && args[0] == "docs")
        {
            return RunDocs(args);
        }

        if (args.Length > 0 && args[0] == "preset")
        {
            return RunPreset(args);
        }

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

    private static int RunDocs(string[] args)
    {
        if (args.Length > 2)
        {
            Console.Error.WriteLine("Error: Usage: temporal-sharp docs [output-file]");
            return 2;
        }

        var output = args.Length == 2 ? args[1] : "RULES.md";
        File.WriteAllText(output, RulesDocGenerator.Generate());
        Console.Out.WriteLine($"Wrote {output}");
        return 0;
    }

    private static int RunPreset(string[] args)
    {
        if (args.Length < 2 || args.Length > 4)
        {
            Console.Error.WriteLine("Error: Usage: temporal-sharp preset <recommended|strict> [--write <file>]");
            return 2;
        }

        var tier = args[1];
        if (tier is not (SeverityPresetGenerator.Recommended or SeverityPresetGenerator.Strict))
        {
            Console.Error.WriteLine($"Error: Unknown preset '{tier}'. Expected 'recommended' or 'strict'.");
            return 2;
        }

        string? writePath = null;
        for (var i = 2; i < args.Length; i++)
        {
            if (args[i] == "--write")
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("Error: Option '--write' requires a file path.");
                    return 2;
                }

                writePath = args[++i];
            }
            else
            {
                Console.Error.WriteLine($"Error: Unknown option '{args[i]}'.");
                return 2;
            }
        }

        var content = SeverityPresetGenerator.Generate(tier);
        if (writePath is not null)
        {
            File.WriteAllText(writePath, content);
            Console.Out.WriteLine($"Wrote {writePath}");
        }
        else
        {
            Console.Out.WriteLine(content);
        }

        return 0;
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
