using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace Kogoshvili.Temporal.Cli.Analysis;

internal static class ProjectLoader
{
    public static async Task<Solution> LoadAsync(string path, CancellationToken cancellationToken)
    {
        if (!MSBuildLocator.IsRegistered)
        {
            MSBuildLocator.RegisterDefaults();
        }

        var workspace = MSBuildWorkspace.Create();
        workspace.WorkspaceFailed += (_, args) =>
            Console.Error.WriteLine($"MSBuild: {args.Diagnostic.Message}");

        if (path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
        {
            return await workspace.OpenSolutionAsync(path, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        if (path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            var project = await workspace.OpenProjectAsync(path, cancellationToken: cancellationToken).ConfigureAwait(false);
            return project.Solution;
        }

        throw new ArgumentException($"Unsupported path '{path}'. Expected a .sln or .csproj file.");
    }

    /// <summary>
    /// Loads multiple solutions/projects, returning one <see cref="Solution"/>
    /// per input. Each input is opened in its own <see cref="MSBuildWorkspace"/>
    /// (a single workspace's <c>OpenSolutionAsync</c> replaces its current
    /// solution rather than merging), and the graph builder stitches across the
    /// resulting solutions by fully-qualified type/method name.
    /// </summary>
    public static async Task<IReadOnlyList<Solution>> LoadAsync(
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken)
    {
        if (!MSBuildLocator.IsRegistered)
        {
            MSBuildLocator.RegisterDefaults();
        }

        var solutions = new List<Solution>(paths.Count);
        foreach (var path in paths)
        {
            var workspace = MSBuildWorkspace.Create();
            workspace.WorkspaceFailed += (_, args) =>
                Console.Error.WriteLine($"MSBuild: {args.Diagnostic.Message}");

            if (path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
            {
                solutions.Add(await workspace.OpenSolutionAsync(path, cancellationToken: cancellationToken).ConfigureAwait(false));
            }
            else if (path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                var project = await workspace.OpenProjectAsync(path, cancellationToken: cancellationToken).ConfigureAwait(false);
                solutions.Add(project.Solution);
            }
            else
            {
                throw new ArgumentException($"Unsupported path '{path}'. Expected a .sln or .csproj file.");
            }
        }

        return solutions;
    }
}
