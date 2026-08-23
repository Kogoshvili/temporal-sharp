using Kogoshvili.Temporal.Cli.Docs;

namespace Kogoshvili.Temporal.Cli.Tests;

public class DocsGenerationTests
{
    [Fact]
    public void GeneratorOutput_MatchesCommittedRulesMd()
    {
        var generated = RulesDocGenerator.Generate();
        var committed = File.ReadAllText(FindRepoFile("RULES.md"));

        Assert.Equal(Normalize(committed), Normalize(generated));
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n");

    private static string FindRepoFile(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, fileName);
            if (File.Exists(candidate) && Directory.Exists(Path.Combine(directory.FullName, "src")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"Could not locate {fileName} from the test output directory.");
    }
}
