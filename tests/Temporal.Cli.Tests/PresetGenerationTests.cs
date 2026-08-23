using Kogoshvili.Temporal.Cli.Presets;

namespace Kogoshvili.Temporal.Cli.Tests;

public class PresetGenerationTests
{
    [Theory]
    [InlineData("recommended")]
    [InlineData("strict")]
    public void GeneratorOutput_MatchesCommittedBundle(string tier)
    {
        var generated = SeverityPresetGenerator.Generate(tier);
        var committed = File.ReadAllText(FindRepoFile($"editorconfig/{tier}.editorconfig"));

        Assert.Equal(Normalize(committed), Normalize(generated));
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n");

    private static string FindRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate) && Directory.Exists(Path.Combine(directory.FullName, "src")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"Could not locate {relativePath} from the test output directory.");
    }
}
