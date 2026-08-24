namespace Kogoshvili.Temporal.Cli.Map;

internal enum MapOutputFormat
{
    Mermaid,
    Json,
    Html,
    Dot,
}

internal sealed class MapOptions
{
    public IReadOnlyList<string> Paths { get; init; } = Array.Empty<string>();

    public MapOutputFormat Format { get; init; } = MapOutputFormat.Mermaid;

    public string? Output { get; init; }

    public static MapOptions Parse(string[] args)
    {
        var paths = new List<string>();
        var format = MapOutputFormat.Mermaid;
        string? output = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "map":
                    break;

                case "--format":
                    format = ParseFormat(RequireValue(args, ref i, "--format"));
                    break;

                case "--output":
                    output = RequireValue(args, ref i, "--output");
                    break;

                default:
                    if (args[i].StartsWith("--", StringComparison.Ordinal))
                    {
                        throw new ArgumentException($"Unknown option '{args[i]}'.");
                    }

                    paths.Add(args[i]);
                    break;
            }
        }

        if (paths.Count == 0)
        {
            throw new ArgumentException("At least one project or solution path is required.");
        }

        return new MapOptions
        {
            Paths = paths,
            Format = format,
            Output = output,
        };
    }

    public static void PrintUsage(TextWriter writer)
    {
        writer.WriteLine("Usage: temporal-sharp map <path.sln|path.csproj|dir> [...] [options]");
        writer.WriteLine();
        writer.WriteLine("Options:");
        writer.WriteLine("  --format <mermaid|json|html|dot>  Output format (default: mermaid).");
        writer.WriteLine("  --output <file>                   Write to a file instead of stdout.");
    }

    /// <summary>
    /// Expands each input to a concrete project/solution file. Files are kept
    /// as-is; a directory is expanded to the solution(s) it contains, or (when
    /// it contains no solution) to all of its project files.
    /// </summary>
    public static IReadOnlyList<string> ResolvePaths(IReadOnlyList<string> paths)
    {
        var resolved = new List<string>();
        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                resolved.Add(System.IO.Path.GetFullPath(path));
                continue;
            }

            if (!Directory.Exists(path))
            {
                throw new ArgumentException($"Path '{path}' does not exist.");
            }

            var solutions = Directory.GetFiles(path, "*.sln");
            if (solutions.Length > 0)
            {
                resolved.AddRange(solutions.Select(System.IO.Path.GetFullPath));
                continue;
            }

            var projects = Directory.GetFiles(path, "*.csproj");
            if (projects.Length == 0)
            {
                throw new ArgumentException($"Directory '{path}' contains no .sln or .csproj file.");
            }

            resolved.AddRange(projects.Select(System.IO.Path.GetFullPath));
        }

        return resolved;
    }

    private static string RequireValue(string[] args, ref int i, string option)
    {
        if (i + 1 >= args.Length)
        {
            throw new ArgumentException($"Option '{option}' requires a value.");
        }

        return args[++i];
    }

    private static MapOutputFormat ParseFormat(string value) => value switch
    {
        "mermaid" => MapOutputFormat.Mermaid,
        "json" => MapOutputFormat.Json,
        "html" => MapOutputFormat.Html,
        "dot" => MapOutputFormat.Dot,
        _ => throw new ArgumentException($"Unknown format '{value}'."),
    };
}
