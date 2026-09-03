using Microsoft.CodeAnalysis;

namespace Kogoshvili.Temporal.Cli.Map;

/// <summary>
/// Heuristics for recognizing test projects (which are excluded from the map
/// by default): a <c>*.Tests.csproj</c>/<c>*.Test.csproj</c> file name, or a
/// reference to a test framework assembly.
/// </summary>
internal static class TestProjectFilter
{
    private static readonly string[] TestFrameworkAssemblies =
    [
        "xunit.core",
        "nunit.framework",
        "Microsoft.VisualStudio.TestPlatform.TestFramework",
        "Microsoft.TestPlatform.TestHost",
    ];

    public static bool IsTestProject(Project project)
    {
        if (!string.IsNullOrEmpty(project.FilePath) && IsTestProjectName(project.FilePath))
        {
            return true;
        }

        return HasTestFrameworkReferences(project.MetadataReferences
            .OfType<PortableExecutableReference>()
            .Select(reference => reference.Display ?? string.Empty));
    }

    public static bool IsTestProjectName(string path)
    {
        var fileName = System.IO.Path.GetFileName(path);
        return fileName.EndsWith(".tests.csproj", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".test.csproj", StringComparison.OrdinalIgnoreCase);
    }

    public static bool HasTestFrameworkReferences(IEnumerable<string> referencePaths)
    {
        foreach (var path in referencePaths)
        {
            var fileName = System.IO.Path.GetFileName(path);
            var name = fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                ? fileName[..^4]
                : fileName;
            foreach (var framework in TestFrameworkAssemblies)
            {
                if (string.Equals(name, framework, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
