namespace Kogoshvili.Temporal.Cli.History;

internal sealed class HistoryDownloadOptions
{
    public string WorkflowType { get; init; } = string.Empty;

    public string? ExecutionStatus { get; init; } = "Completed";

    public int? Limit { get; init; }

    public string OutDir { get; init; } = string.Empty;

    public string? Config { get; init; }

    public static HistoryDownloadOptions Parse(string[] args)
    {
        string? workflowType = null;
        string? executionStatus = "Completed";
        int? limit = null;
        string? outDir = null;
        string? config = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "history":
                case "download":
                    break;

                case "--execution-status":
                    executionStatus = RequireValue(args, ref i, "--execution-status");
                    break;

                case "--limit":
                    limit = ParseLimit(RequireValue(args, ref i, "--limit"));
                    break;

                case "--out":
                    outDir = RequireValue(args, ref i, "--out");
                    break;

                case "--config":
                    config = RequireValue(args, ref i, "--config");
                    break;

                default:
                    if (args[i].StartsWith("--", StringComparison.Ordinal))
                    {
                        throw new ArgumentException($"Unknown option '{args[i]}'.");
                    }

                    if (workflowType is not null)
                    {
                        throw new ArgumentException("Only one workflow type may be specified.");
                    }

                    workflowType = args[i];
                    break;
            }
        }

        if (workflowType is null)
        {
            throw new ArgumentException("A workflow type is required.");
        }

        if (outDir is null)
        {
            throw new ArgumentException("Option '--out' is required.");
        }

        return new HistoryDownloadOptions
        {
            WorkflowType = workflowType,
            ExecutionStatus = executionStatus,
            Limit = limit,
            OutDir = outDir,
            Config = config,
        };
    }

    public static void PrintUsage(TextWriter writer)
    {
        writer.WriteLine("Usage: temporal-sharp history download <workflowType> [options]");
        writer.WriteLine();
        writer.WriteLine("Downloads recorded workflow histories as *.json files for later replay.");
        writer.WriteLine();
        writer.WriteLine("Options:");
        writer.WriteLine("  --execution-status <status>  Filter by execution status (default: Completed).");
        writer.WriteLine("  --limit <n>                  Maximum number of histories to download.");
        writer.WriteLine("  --out <dir>                  Directory to write *.json histories into (required).");
        writer.WriteLine("  --config <path>              JSON config file (default: appsettings.json + Temporal__* env vars).");
    }

    private static int? ParseLimit(string value)
    {
        if (!int.TryParse(value, out var limit) || limit < 0)
        {
            throw new ArgumentException($"Invalid limit '{value}'. Expected a non-negative integer.");
        }

        return limit;
    }

    private static string RequireValue(string[] args, ref int i, string option)
    {
        if (i + 1 >= args.Length)
        {
            throw new ArgumentException($"Option '{option}' requires a value.");
        }

        return args[++i];
    }
}
