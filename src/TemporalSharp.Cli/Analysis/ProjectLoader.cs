using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace TemporalSharp.Cli.Analysis;

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
}
