using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis;

namespace Kogoshvili.Temporal.Cli.Reporting;

internal sealed record DiagnosticDto(
    string Id,
    string Severity,
    string Message,
    string? File,
    int? StartLine,
    int? StartColumn);

internal static class Reporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static void WriteConsole(
        TextWriter writer,
        IReadOnlyList<Diagnostic> diagnostics,
        IReadOnlyDictionary<string, DiagnosticSeverity>? severityOverrides = null)
    {
        foreach (var dto in Sort(ToDtos(diagnostics, severityOverrides)))
        {
            var location = dto.File is null
                ? "<no location>"
                : $"{dto.File}({dto.StartLine},{dto.StartColumn})";
            writer.WriteLine($"{location}: {dto.Severity} {dto.Id}: {dto.Message}");
        }
    }

    public static string ToJson(
        IReadOnlyList<Diagnostic> diagnostics,
        IReadOnlyDictionary<string, DiagnosticSeverity>? severityOverrides = null)
        => JsonSerializer.Serialize(Sort(ToDtos(diagnostics, severityOverrides)), JsonOptions);

    public static string ToSarif(
        IReadOnlyList<Diagnostic> diagnostics,
        IReadOnlyDictionary<string, DiagnosticSeverity>? severityOverrides = null)
    {
        var dtos = Sort(ToDtos(diagnostics, severityOverrides));

        var rules = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        var results = new JsonArray();

        foreach (var dto in dtos)
        {
            if (!rules.ContainsKey(dto.Id))
            {
                rules[dto.Id] = new JsonObject { ["id"] = dto.Id };
            }

            var result = new JsonObject
            {
                ["ruleId"] = dto.Id,
                ["level"] = ToSarifLevel(dto.Severity),
                ["message"] = new JsonObject { ["text"] = dto.Message },
            };

            if (dto.File is not null)
            {
                var region = new JsonObject
                {
                    ["startLine"] = dto.StartLine ?? 1,
                    ["startColumn"] = dto.StartColumn ?? 1,
                };

                result["locations"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["physicalLocation"] = new JsonObject
                        {
                            ["artifactLocation"] = new JsonObject { ["uri"] = dto.File },
                            ["region"] = region,
                        },
                    },
                };
            }

            results.Add(result);
        }

        var driver = new JsonObject
        {
            ["name"] = "Kogoshvili.Temporal",
            ["rules"] = new JsonArray(rules.Values.ToArray()),
        };

        var root = new JsonObject
        {
            ["version"] = "2.1.0",
            ["$schema"] = "https://json.schemastore.org/sarif-2.1.0.json",
            ["runs"] = new JsonArray
            {
                new JsonObject
                {
                    ["tool"] = new JsonObject { ["driver"] = driver },
                    ["results"] = results,
                },
            },
        };

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static IReadOnlyList<DiagnosticDto> ToDtos(
        IReadOnlyList<Diagnostic> diagnostics,
        IReadOnlyDictionary<string, DiagnosticSeverity>? severityOverrides)
    {
        var dtos = new List<DiagnosticDto>(diagnostics.Count);
        foreach (var diagnostic in diagnostics)
        {
            var severity = diagnostic.Severity;
            if (severityOverrides is not null && severityOverrides.TryGetValue(diagnostic.Id, out var overridden))
            {
                severity = overridden;
            }

            var lineSpan = diagnostic.Location.GetLineSpan();
            var path = lineSpan.Path;
            dtos.Add(new DiagnosticDto(
                diagnostic.Id,
                severity.ToString().ToLowerInvariant(),
                diagnostic.GetMessage(),
                string.IsNullOrEmpty(path) ? null : path,
                lineSpan.IsValid ? lineSpan.StartLinePosition.Line + 1 : null,
                lineSpan.IsValid ? lineSpan.StartLinePosition.Character + 1 : null));
        }

        return dtos;
    }

    private static List<DiagnosticDto> Sort(IReadOnlyList<DiagnosticDto> dtos)
    {
        var sorted = new List<DiagnosticDto>(dtos);
        sorted.Sort(static (a, b) =>
        {
            var byFile = string.CompareOrdinal(a.File ?? string.Empty, b.File ?? string.Empty);
            if (byFile != 0)
            {
                return byFile;
            }

            var byLine = (a.StartLine ?? 0).CompareTo(b.StartLine ?? 0);
            if (byLine != 0)
            {
                return byLine;
            }

            return (a.StartColumn ?? 0).CompareTo(b.StartColumn ?? 0);
        });

        return sorted;
    }

    private static string ToSarifLevel(string severity) => severity switch
    {
        "error" => "error",
        "warning" => "warning",
        "info" => "note",
        _ => "none",
    };
}
