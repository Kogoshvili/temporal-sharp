namespace Kogoshvili.Temporal.Cli.Map;

/// <summary>
/// Detects projects that have not been NuGet-restored. MSBuildWorkspace loads
/// unrestored projects without a compilation, which used to produce a
/// silently empty graph — now it is a hard, actionable error.
/// </summary>
public static class ProjectRestoreCheck
{
    /// <summary>
    /// Expands each input (a .csproj, a .sln, or anything else — non-project
    /// paths are ignored) to its contained project files and returns those
    /// whose <c>obj/project.assets.json</c> is missing, i.e. projects that
    /// need <c>dotnet restore</c>.
    /// </summary>
    public static IReadOnlyList<string> FindUnrestoredProjects(IEnumerable<string> paths)
    {
        var unrestored = new List<string>();
        foreach (var path in paths)
        {
            foreach (var project in ExpandToProjects(path))
            {
                var assets = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(project) ?? ".",
                    "obj",
                    "project.assets.json");
                if (!System.IO.File.Exists(assets))
                {
                    unrestored.Add(project);
                }
            }
        }

        return unrestored;
    }

    private static IEnumerable<string> ExpandToProjects(string path)
    {
        if (path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            yield return path;
            yield break;
        }

        if (!path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        var slnDir = System.IO.Path.GetDirectoryName(path) ?? ".";
        foreach (var line in System.IO.File.ReadLines(path))
        {
            // Project("{TYPE-GUID}") = "Name", "Relative\Path.csproj", "{GUID}"
            var match = System.Text.RegularExpressions.Regex.Match(
                line, "^Project\\(.*\\)\\s*=\\s*\".*\",\\s*\"(.*\\.csproj)\"");
            if (match.Success)
            {
                var relative = match.Groups[1].Value.Replace('\\', System.IO.Path.DirectorySeparatorChar);
                yield return System.IO.Path.GetFullPath(System.IO.Path.Combine(slnDir, relative));
            }
        }
    }
}
