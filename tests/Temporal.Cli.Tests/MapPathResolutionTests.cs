using Kogoshvili.Temporal.Cli.Map;

namespace Kogoshvili.Temporal.Cli.Tests;

/// <summary>
/// Tests for MapOptions.ResolvePaths: recursive directory scanning with
/// solution-wins deduplication and the --max-depth option.
/// </summary>
public class MapPathResolutionTests : IDisposable
{
    private static readonly string Root = Path.Combine(
        Path.GetTempPath(), "temporal-sharp-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }

    private string WriteFile(params string[] relativeSegments)
    {
        var path = Path.Combine(new[] { Root }.Concat(relativeSegments).ToArray());
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Empty);
        return path;
    }

    private static string WriteSolution(string path, params string[] projectPaths)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var lines = new List<string> { "Microsoft Visual Studio Solution File, Format Version 12.00" };
        foreach (var project in projectPaths)
        {
            lines.Add($"Project(\"{{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}}\") = \"P\", \"{project}\", \"{{00000000-0000-0000-0000-000000000000}}\"");
            lines.Add("EndProject");
        }

        lines.Add("Global");
        lines.Add("EndGlobal");
        File.WriteAllLines(path, lines);
        return path;
    }

    [Fact]
    public void DirectoryScanFindsNestedProjectsAndDropsSlnMembers()
    {
        var sln = WriteSolution(Path.Combine(Root, "A.sln"), Path.Combine("src", "App", "App.csproj"));
        var member = WriteFile("src", "App", "App.csproj");
        var orphan = WriteFile("lib", "Lib.csproj");
        var deep = WriteFile("deep", "a", "Deep.csproj");

        var resolved = MapOptions.ResolvePaths([Root], maxDepth: 5);

        Assert.Equal(3, resolved.Count);
        Assert.Contains(sln, resolved);
        Assert.Contains(orphan, resolved);
        Assert.Contains(deep, resolved);
        Assert.DoesNotContain(member, resolved);
        // Solutions come first, then orphan projects.
        Assert.Equal(sln, resolved[0]);
    }

    [Fact]
    public void MaxDepthLimitsRecursion()
    {
        var sln = WriteSolution(Path.Combine(Root, "sub", "Deep.sln"), "X.csproj");
        WriteFile("sub", "X.csproj");

        var found = MapOptions.ResolvePaths([Root], maxDepth: 2);
        Assert.Contains(sln, found);
        Assert.DoesNotContain(Path.Combine(Root, "sub", "X.csproj"), found);

        Assert.Throws<ArgumentException>(() => MapOptions.ResolvePaths([Root], maxDepth: 1));
    }

    [Fact]
    public void MaxDepthOptionDefaultsToFiveAndValidates()
    {
        Assert.Equal(5, MapOptions.Parse(new[] { "some.sln" }).MaxDepth);
        Assert.Equal(3, MapOptions.Parse(new[] { "some.sln", "--max-depth", "3" }).MaxDepth);

        Assert.Throws<ArgumentException>(() => MapOptions.Parse(new[] { "some.sln", "--max-depth", "0" }));
        Assert.Throws<ArgumentException>(() => MapOptions.Parse(new[] { "some.sln", "--max-depth", "abc" }));
        Assert.Throws<ArgumentException>(() => MapOptions.Parse(new[] { "some.sln", "--max-depth" }));
    }

    [Fact]
    public void ExplicitFileArgsAreNeverDropped()
    {
        var sln = WriteSolution(Path.Combine(Root, "A.sln"), Path.Combine("src", "App", "App.csproj"));
        var member = WriteFile("src", "App", "App.csproj");

        var resolved = MapOptions.ResolvePaths([sln, member], maxDepth: 5);

        Assert.Equal(new[] { sln, member }, resolved);
    }

    [Fact]
    public void BuildOutputDirectoriesAreSkipped()
    {
        var sln = WriteSolution(Path.Combine(Root, "Sln.sln"), Path.Combine("src", "App.csproj"));
        WriteFile("src", "App.csproj");
        WriteFile("bin", "B.csproj");
        WriteFile("obj", "O.csproj");
        WriteFile("artifacts", "P.csproj");
        WriteFile(".git", "Q.csproj");
        var keep = WriteFile("keep.csproj");

        var resolved = MapOptions.ResolvePaths([Root], maxDepth: 5);

        Assert.Equal(new[] { sln, keep }, resolved);
    }

    [Fact]
    public void DuplicateAndOverlappingInputsAreDeduplicated()
    {
        var shared = WriteFile("shared", "Shared.csproj");
        var slnA = WriteSolution(Path.Combine(Root, "RepoA", "A.sln"), Path.Combine("..", "shared", "Shared.csproj"));
        var slnB = WriteSolution(Path.Combine(Root, "RepoB", "B.sln"), Path.Combine("..", "shared", "Shared.csproj"));

        // Same directory twice, plus each solution file listed again explicitly.
        var resolved = MapOptions.ResolvePaths(
            [Root, Path.Combine(Root, "RepoA"), slnA, slnB], maxDepth: 5);

        // Shared.csproj belongs to both solutions, so it never appears loose.
        Assert.DoesNotContain(shared, resolved);
        Assert.Equal(new[] { slnA, slnB }, resolved);
    }
}
