using Microsoft.CodeAnalysis;

namespace TemporalSharp.Cli;

internal enum OutputFormat
{
    Console,
    Json,
    Sarif,
}

internal sealed class Options
{
    public string Path { get; init; } = string.Empty;

    public OutputFormat Format { get; init; } = OutputFormat.Console;

    // null means "do not fail" regardless of findings.
    public DiagnosticSeverity? FailOn { get; init; }

    public static Options Parse(string[] args)
    {
        string? path = null;
        var format = OutputFormat.Console;
        DiagnosticSeverity? failOn = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--format":
                    format = ParseFormat(RequireValue(args, ref i, "--format"));
                    break;

                case "--fail-on":
                    failOn = ParseSeverity(RequireValue(args, ref i, "--fail-on"));
                    break;

                default:
                    if (args[i].StartsWith("--", StringComparison.Ordinal))
                    {
                        throw new ArgumentException($"Unknown option '{args[i]}'.");
                    }

                    if (path is not null)
                    {
                        throw new ArgumentException("Only one project or solution path may be specified.");
                    }

                    path = args[i];
                    break;
            }
        }

        if (path is null)
        {
            throw new ArgumentException("A project or solution path is required.");
        }

        return new Options { Path = path, Format = format, FailOn = failOn };
    }

    public static void PrintUsage(TextWriter writer)
    {
        writer.WriteLine("Usage: temporal-sharp analyze <path.sln|path.csproj> [options]");
        writer.WriteLine();
        writer.WriteLine("Options:");
        writer.WriteLine("  --format <console|json|sarif>  Output format (default: console).");
        writer.WriteLine("  --fail-on <none|info|warning|error>  Exit non-zero when a diagnostic of the");
        writer.WriteLine("                                 given severity or higher is found (default: none).");
    }

    private static string RequireValue(string[] args, ref int i, string option)
    {
        if (i + 1 >= args.Length)
        {
            throw new ArgumentException($"Option '{option}' requires a value.");
        }

        return args[++i];
    }

    private static OutputFormat ParseFormat(string value) => value switch
    {
        "console" => OutputFormat.Console,
        "json" => OutputFormat.Json,
        "sarif" => OutputFormat.Sarif,
        _ => throw new ArgumentException($"Unknown format '{value}'."),
    };

    private static DiagnosticSeverity? ParseSeverity(string value) => value switch
    {
        "none" => null,
        "info" => DiagnosticSeverity.Info,
        "warning" => DiagnosticSeverity.Warning,
        "error" => DiagnosticSeverity.Error,
        _ => throw new ArgumentException($"Unknown severity '{value}'."),
    };
}
