using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kogoshvili.Temporal.Analyzers.Tests;

/// <summary>
/// Guards against internal inconsistency between the descriptor catalog
/// (<c>DiagnosticDescriptors.cs</c>) and the analyzers that report them:
/// unique ids, no orphan descriptors, no analyzer declaring an unknown rule,
/// and valid metadata on every descriptor.
/// </summary>
public class ConsistencyTests
{
    private static readonly string[] ValidCategories =
    {
        "Determinism", "WorkflowState", "SdkMisuse", "BestPractice", "Testing",
    };

    [Fact]
    public void DescriptorIdsAreUnique()
    {
        var ids = Descriptors().Select(d => d.Id).ToList();
        var duplicates = ids.GroupBy(id => id, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.True(duplicates.Count == 0, $"Duplicate descriptor ids: {string.Join(", ", duplicates)}");
    }

    [Fact]
    public void EveryDescriptorIsDeclaredByAnAnalyzer()
    {
        var declared = Analyzers()
            .SelectMany(a => a.SupportedDiagnostics)
            .Select(d => d.Id)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var descriptor in Descriptors())
        {
            Assert.True(declared.Contains(descriptor.Id), $"Descriptor {descriptor.Id} is not declared by any analyzer");
        }
    }

    [Fact]
    public void EveryAnalyzerDeclaredRuleHasADescriptor()
    {
        var descriptorIds = Descriptors().Select(d => d.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var analyzer in Analyzers())
        {
            foreach (var diagnostic in analyzer.SupportedDiagnostics)
            {
                Assert.True(descriptorIds.Contains(diagnostic.Id),
                    $"{analyzer.GetType().Name} declares unknown rule {diagnostic.Id}");
            }
        }
    }

    [Fact]
    public void EveryDescriptorHasValidMetadata()
    {
        foreach (var descriptor in Descriptors())
        {
            Assert.StartsWith("TMP", descriptor.Id, StringComparison.Ordinal);
            Assert.Contains(descriptor.Category, ValidCategories);
            Assert.False(string.IsNullOrWhiteSpace(descriptor.Title.ToString()), $"{descriptor.Id} has no title");
            Assert.False(string.IsNullOrWhiteSpace(descriptor.MessageFormat.ToString()), $"{descriptor.Id} has no message format");
            Assert.False(string.IsNullOrWhiteSpace(descriptor.Description.ToString()), $"{descriptor.Id} has no description");
        }
    }

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

    private static IReadOnlyList<DiagnosticAnalyzer> Analyzers()
    {
        var assembly = typeof(Analyzers.DeterminismAnalyzer).Assembly;
        var analyzers = new List<DiagnosticAnalyzer>();

        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || !typeof(DiagnosticAnalyzer).IsAssignableFrom(type))
            {
                continue;
            }

            if (type.GetCustomAttribute<DiagnosticAnalyzerAttribute>() is null)
            {
                continue;
            }

            if (Activator.CreateInstance(type) is DiagnosticAnalyzer analyzer)
            {
                analyzers.Add(analyzer);
            }
        }

        return analyzers;
    }
}
