namespace Kogoshvili.Temporal.Cli.Map;

internal enum MapOutputFormat
{
    Mermaid,
    Json,
    Html,
    Dot,
    Markdown,
}

internal sealed class MapOptions
{
    public IReadOnlyList<string> Paths { get; init; } = Array.Empty<string>();

    public MapOutputFormat Format { get; init; } = MapOutputFormat.Mermaid;

    public string? Output { get; init; }

    public int MaxDepth { get; init; } = 5;

    /// <summary>
    /// When false (the default), test projects are excluded from the graph.
    /// </summary>
    public bool IncludeTests { get; init; }

    /// <summary>
    /// Renders handler signatures, return types, and call-site options
    /// (timeouts, retry). Default on; disabled with --no-contracts.
    /// </summary>
    public bool Contracts { get; init; } = true;

    public static MapOptions Parse(string[] args)
    {
        var paths = new List<string>();
        var format = MapOutputFormat.Mermaid;
        string? output = null;
        var includeTests = false;
        var includeContracts = true;
        var maxDepth = 5;

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

                case "--include-tests":
                    includeTests = true;
                    break;

                case "--no-contracts":
                    includeContracts = false;
                    break;

                case "--max-depth":
                    maxDepth = ParseMaxDepth(RequireValue(args, ref i, "--max-depth"));
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
            IncludeTests = includeTests,
            Contracts = includeContracts,
            MaxDepth = maxDepth,
        };
    }

    public static void PrintUsage(TextWriter writer)
    {
        writer.WriteLine("Usage: temporal-sharp map <path.sln|path.csproj|dir> [...] [options]");
        writer.WriteLine();
        writer.WriteLine("Options:");
        writer.WriteLine("  --format <mermaid|json|html|dot|markdown>  Output format (default: mermaid).");
        writer.WriteLine("  --output <file>                   Write to a file instead of stdout.");
        writer.WriteLine("  --include-tests                   Keep test projects in the graph (excluded by default).");
        writer.WriteLine("  --no-contracts                    Hide signatures/return types and call options.");
        writer.WriteLine("  --max-depth <n>                   Directory scan depth (default: 5).");
    }

    /// <summary>
    /// Expands each input to a concrete project/solution file. Files are kept
    /// as-is; a directory is scanned recursively (up to <paramref name="maxDepth"/>
    /// levels below it) for solutions and projects. Projects referenced by any
    /// discovered solution are dropped, so a repo dropped into a directory is
    /// represented by its solution alone and a project is never loaded twice.
    /// </summary>
    public static IReadOnlyList<string> ResolvePaths(IReadOnlyList<string> paths, int maxDepth)
    {
        var resolved = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var skipped = 0;

        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                AddUnique(Path.GetFullPath(path));
                continue;
            }

            if (!Directory.Exists(path))
            {
                throw new ArgumentException($"Path '{path}' does not exist.");
            }

            var root = System.IO.Path.GetFullPath(path);
            var solutions = ScanDirectory(root, maxDepth, ".sln");
            var projects = ScanDirectory(root, maxDepth, ".csproj");

            var inSolutions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var solution in solutions)
            {
                foreach (var project in SolutionProjectPaths(solution))
                {
                    inSolutions.Add(project);
                }
            }

            skipped += projects.Count(p => !inSolutions.Contains(p));
            if (solutions.Count == 0 && projects.Count == 0)
            {
                throw new ArgumentException(
                    $"Directory '{path}' contains no .sln or .csproj file (searched {maxDepth} level(s) deep).");
            }

            solutions.ForEach(AddUnique);
            foreach (var project in projects.Where(p => !inSolutions.Contains(p)))
            {
                AddUnique(project);
            }
        }

        if (skipped > 0)
        {
            Console.Error.WriteLine($"Skipped {skipped} project(s): already referenced by a discovered solution.");
        }

        return resolved;

        void AddUnique(string path)
        {
            if (seen.Add(path))
            {
                resolved.Add(path);
            }
        }
    }

    /// <summary>
    /// Directories that never contain relevant project files: build output,
    /// package caches, and (via the dot-prefix rule) tool/VCS folders.
    /// </summary>
    private static readonly HashSet<string> SkippedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin",
        "obj",
        "artifacts",
        "node_modules",
        "packages",
    };

    /// <summary>
    /// Collects files matching <c>*<paramref name="extension"/></c> under
    /// <paramref name="root"/>, descending at most <paramref name="maxDepth"/>
    /// directory levels (files directly in the root are at depth 1). Hidden
    /// directories and build output are skipped.
    /// </summary>
    private static List<string> ScanDirectory(string root, int maxDepth, string extension)
    {
        var found = new List<string>();
        var pending = new List<(string Directory, int Depth)> { (root, 1) };
        while (pending.Count > 0)
        {
            var (directory, depth) = pending[^1];
            pending.RemoveAt(pending.Count - 1);

            found.AddRange(Directory.EnumerateFiles(directory, "*" + extension));

            if (depth >= maxDepth)
            {
                continue;
            }

            foreach (var sub in Directory.EnumerateDirectories(directory))
            {
                var name = System.IO.Path.GetFileName(sub);
                if (name.StartsWith('.') || SkippedDirectories.Contains(name))
                {
                    continue;
                }

                pending.Add((sub, depth + 1));
            }
        }

        return found;
    }

    /// <summary>
    /// Extracts the absolute paths of the C# projects referenced by a solution
    /// file, normalizing solution-folder-relative backslash paths.
    /// </summary>
    private static IEnumerable<string> SolutionProjectPaths(string solutionPath)
    {
        var solutionDir = System.IO.Path.GetDirectoryName(solutionPath)!;
        foreach (var line in File.ReadLines(solutionPath))
        {
            if (!line.TrimStart().StartsWith("Project(", StringComparison.Ordinal))
            {
                continue;
            }

            var parts = line.Split('"');
            if (parts.Length < 6)
            {
                continue;
            }

            var projectPath = parts[5];
            if (!projectPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return System.IO.Path.GetFullPath(
                System.IO.Path.Combine(solutionDir, projectPath.Replace('\\', System.IO.Path.DirectorySeparatorChar)));
        }
    }

    private static int ParseMaxDepth(string value)
    {
        if (!int.TryParse(value, out var depth) || depth < 1)
        {
            throw new ArgumentException($"Invalid --max-depth value '{value}'. Expected an integer of 1 or more.");
        }

        return depth;
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
        "markdown" or "md" => MapOutputFormat.Markdown,
        _ => throw new ArgumentException($"Unknown format '{value}'."),
    };
}
