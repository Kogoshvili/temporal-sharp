using System.Reflection;
using Microsoft.CodeAnalysis;

namespace Kogoshvili.Temporal.Analyzers.Tests;

/// <summary>
/// Guards against drift between <c>DiagnosticDescriptors.cs</c> and the
/// human-readable rule catalog in <c>RULES.md</c>. Adding a rule without
/// documenting it (or vice versa) fails the build.
/// </summary>
public class DocumentationSyncTests
{
    private static readonly IReadOnlyDictionary<string, string[]> CategoryToSections =
        new Dictionary<string, string[]>
        {
            ["Determinism"] = new[] { "Determinism" },
            ["WorkflowState"] = new[] { "Shared-state mutation" },
            ["SdkMisuse"] = new[] { "SDK feature-misuse", ".NET-specific" },
            ["BestPractice"] = new[] { "Best practice" },
            ["Testing"] = new[] { "Testing" },
        };

    [Fact]
    public void EveryDescriptorIsDocumentedWithMatchingSeverityAndCategory()
    {
        var documented = ParseRules(RulesPath());

        foreach (var descriptor in Descriptors())
        {
            Assert.True(
                documented.TryGetValue(descriptor.Id, out var entry),
                $"Rule {descriptor.Id} is missing from RULES.md");

            Assert.Equal(DocumentedSeverity(descriptor), entry.Default);
            Assert.Contains(entry.Section, CategoryToSections[descriptor.Category]);
        }
    }

    [Fact]
    public void EveryDocumentedRuleHasADescriptor()
    {
        var descriptorIds = Descriptors().Select(d => d.Id).ToHashSet();

        foreach (var id in ParseRules(RulesPath()).Keys)
        {
            Assert.True(descriptorIds.Contains(id), $"RULES.md documents {id}, which has no descriptor");
        }
    }

    private static string DocumentedSeverity(DiagnosticDescriptor descriptor) =>
        descriptor.IsEnabledByDefault ? descriptor.DefaultSeverity.ToString() : "off";

    private static IReadOnlyList<DiagnosticDescriptor> Descriptors()
    {
        var assembly = typeof(Analyzers.DeterminismAnalyzer).Assembly;
        var type = assembly.GetType("Kogoshvili.Temporal.Analyzers.Diagnostics.DiagnosticDescriptors")
            ?? throw new InvalidOperationException("DiagnosticDescriptors type not found");

        return type
            .GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(f => f.FieldType == typeof(DiagnosticDescriptor))
            .Select(f => (DiagnosticDescriptor)f.GetValue(null)!)
            .OrderBy(d => d.Id, StringComparer.Ordinal)
            .ToList();
    }

    private static Dictionary<string, (string Section, string Default)> ParseRules(string rulesPath)
    {
        var result = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
        string? section = null;

        foreach (var line in File.ReadAllLines(rulesPath))
        {
            if (line.StartsWith("## "))
            {
                section = line[3..].Trim();
                continue;
            }

            if (!line.StartsWith('|'))
            {
                continue;
            }

            var cells = line
                .Split('|')
                .Select(c => c.Trim())
                .Where(c => c.Length > 0)
                .ToArray();

            if (cells.Length < 2 || !cells[0].StartsWith("TMP"))
            {
                continue;
            }

            result[cells[0]] = (section ?? string.Empty, cells[1]);
        }

        return result;
    }

    private static string RulesPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "RULES.md");
            if (File.Exists(candidate) && Directory.Exists(Path.Combine(directory.FullName, "src")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate RULES.md from the test output directory.");
    }
}
