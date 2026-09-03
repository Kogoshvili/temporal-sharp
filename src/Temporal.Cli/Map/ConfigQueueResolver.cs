using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Kogoshvili.Temporal.Cli.Map;

/// <summary>
/// Best-effort resolution of queue names that are not string constants:
/// env-var helpers with a constant fallback (<c>GetEnvVarWithDefault("K", "q")</c>)
/// and configuration indexer keys (<c>config["Temporal:Worker:TaskQueue"]</c>)
/// resolved against the declaring project's <c>appsettings.json</c> and
/// <c>appsettings.Production.json</c> (never <c>appsettings.Development.json</c>).
/// </summary>
internal static class ConfigQueueResolver
{
    public static string? Resolve(ExpressionSyntax expression, SemanticModel model)
    {
        // Env-var helper: any invocation whose arguments end in a string
        // constant and whose method name suggests environment lookup.
        if (expression is InvocationExpressionSyntax invocation &&
            invocation.Expression is MemberAccessExpressionSyntax or IdentifierNameSyntax &&
            invocation.ArgumentList.Arguments.Count >= 2)
        {
            var name = invocation.Expression switch
            {
                MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
                IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                _ => string.Empty,
            };
            if (name.Contains("EnvVar", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("EnvironmentVariable", StringComparison.OrdinalIgnoreCase))
            {
                var last = invocation.ArgumentList.Arguments[^1];
                if (TryGetStringConstantValue(last.Expression, model, out var fallback))
                {
                    return fallback;
                }
            }
        }

        // Configuration key path: config["A:B"] or config.GetSection("A")["B"].
        var keyPath = ExtractConfigKey(expression, model);
        if (keyPath is null)
        {
            return null;
        }

        var treeDirectory = System.IO.Path.GetDirectoryName(
            expression.SyntaxTree.FilePath is { Length: > 0 } filePath ? filePath : null);
        if (treeDirectory is null)
        {
            return null;
        }

        var baseSettings = System.IO.Path.Combine(treeDirectory, "appsettings.json");
        var production = System.IO.Path.Combine(treeDirectory, "appsettings.Production.json");
        return NavigateJson(
            ReadIfExists(baseSettings),
            keyPath,
            ReadIfExists(production));
    }

    private static string? ReadIfExists(string path) =>
        System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : null;

    /// <summary>
    /// Extracts a configuration key path from indexer chains:
    /// <c>Configuration["A:B"]</c> → "A:B"; <c>Configuration.GetSection("A")["B"]</c> → "A:B".
    /// </summary>
    private static string? ExtractConfigKey(ExpressionSyntax expression, SemanticModel model)
    {
        var segments = new List<string>();
        var current = expression;
        while (true)
        {
            if (current is ElementAccessExpressionSyntax element &&
                element.ArgumentList.Arguments.Count == 1 &&
                TryGetStringConstantValue(element.ArgumentList.Arguments[0].Expression, model, out var indexed))
            {
                segments.Insert(0, indexed);
                current = element.Expression;
                continue;
            }

            if (current is MemberAccessExpressionSyntax member &&
                member.Name.Identifier.ValueText == "GetSection" &&
                member.Expression is InvocationExpressionSyntax getSection &&
                getSection.ArgumentList.Arguments.Count == 1 &&
                TryGetStringConstantValue(getSection.ArgumentList.Arguments[0].Expression, model, out var section))
            {
                segments.Insert(0, section);
                current = getSection.Expression;
                continue;
            }

            break;
        }

        return segments.Count > 0 ? string.Join(":", segments) : null;
    }

    private static bool TryGetStringConstantValue(ExpressionSyntax expression, SemanticModel? model, out string value)
    {
        if (expression is LiteralExpressionSyntax literal &&
            literal.Token.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StringLiteralToken))
        {
            value = literal.Token.ValueText;
            return true;
        }

        if (model is not null)
        {
            var constant = model.GetConstantValue(expression);
            if (constant.HasValue && constant.Value is string stringValue)
            {
                value = stringValue;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    /// <summary>
    /// Navigates a JSON document by a colon-separated key path
    /// (case-insensitive), overlaying <paramref name="overrideJson"/> (e.g.
    /// appsettings.Production.json) when it defines the same path.
    /// </summary>
    public static string? NavigateJson(string? json, string keyPath, string? overrideJson = null)
    {
        var fromOverride = Navigate(overrideJson, keyPath);
        if (fromOverride is not null)
        {
            return fromOverride;
        }

        return Navigate(json, keyPath);
    }

    private static string? Navigate(string? json, string keyPath)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(json);
            var current = document.RootElement;
            foreach (var segment in keyPath.Split(':'))
            {
                if (current.ValueKind != System.Text.Json.JsonValueKind.Object ||
                    !current.TryGetProperty(segment, out current))
                {
                    return null;
                }
            }

            return current.ValueKind == System.Text.Json.JsonValueKind.String
                ? current.GetString()
                : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
