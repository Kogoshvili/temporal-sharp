using Microsoft.CodeAnalysis;

namespace Kogoshvili.Temporal.Cli;

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

    public IReadOnlyDictionary<string, DiagnosticSeverity> SeverityOverrides { get; init; } =
        new Dictionary<string, DiagnosticSeverity>();

    public static Options Parse(string[] args)
    {
        string? path = null;
        var format = OutputFormat.Console;
        DiagnosticSeverity? failOn = null;
        var severityOverrides = new Dictionary<string, DiagnosticSeverity>(StringComparer.Ordinal);

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "analyze":
                    break;

                case "--format":
                    format = ParseFormat(RequireValue(args, ref i, "--format"));
                    break;

                case "--fail-on":
                    failOn = ParseSeverity(RequireValue(args, ref i, "--fail-on"));
                    break;

                case "--severity":
                    ParseSeverityOverride(RequireValue(args, ref i, "--severity"), severityOverrides);
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

        return new Options
        {
            Path = path,
            Format = format,
            FailOn = failOn,
            SeverityOverrides = severityOverrides,
        };
    }

    public static void PrintUsage(TextWriter writer)
    {
        writer.WriteLine("Usage: temporal-sharp analyze <path.sln|path.csproj> [options]");
        writer.WriteLine();
        writer.WriteLine("Options:");
        writer.WriteLine("  --format <console|json|sarif>  Output format (default: console).");
        writer.WriteLine("  --fail-on <none|info|warning|error>  Exit non-zero when a diagnostic of the");
        writer.WriteLine("                                 given severity or higher is found (default: none).");
        writer.WriteLine("  --severity <TMPxxxx=severity>  Override the severity of a rule (repeatable).");
    }

    private static void ParseSeverityOverride(
        string value,
        IDictionary<string, DiagnosticSeverity> overrides)
    {
        var parts = value.Split('=', 2);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]))
        {
            throw new ArgumentException($"Invalid --severity value '{value}'. Expected RULEID=severity.");
        }

        var severity = ParseSeverity(parts[1]);
        if (severity is null)
        {
            throw new ArgumentException($"Invalid --severity value '{value}'. Expected a severity of info, warning, or error.");
        }

        overrides[parts[0]] = severity.Value;
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
