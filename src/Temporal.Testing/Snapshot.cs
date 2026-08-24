using System.Text.Json;
using Temporalio.Common;

namespace Kogoshvili.Temporal.Testing;

/// <summary>
/// Helpers for capturing and comparing workflow event-history snapshots as JSON.
/// </summary>
public static class Snapshot
{
    /// <summary>Serializes a workflow history to its JSON snapshot form.</summary>
    public static string ToJson(WorkflowHistory history) => history.ToJson();

    /// <summary>Rehydrates a snapshot produced by <see cref="ToJson"/>.</summary>
    public static WorkflowHistory FromJson(string json, string workflowId) =>
        WorkflowHistory.FromJson(workflowId, json);

    /// <summary>
    /// Compares two history snapshots for structural equality, ignoring object
    /// key order and insignificant whitespace. Throws <see cref="ReplayMismatchException"/>
    /// when the histories diverge.
    /// </summary>
    public static void AssertEquivalent(string expectedJson, string actualJson)
    {
        if (AreEquivalent(expectedJson, actualJson))
        {
            return;
        }

        throw new ReplayMismatchException(
            "History snapshots differ." + Environment.NewLine +
            "--- expected ---" + Environment.NewLine + expectedJson + Environment.NewLine +
            "--- actual ---" + Environment.NewLine + actualJson);
    }

    /// <summary>
    /// Returns true when the two snapshots are structurally equal (JSON objects
    /// and arrays compared element-by-element, ignoring key order).
    /// </summary>
    public static bool AreEquivalent(string expectedJson, string actualJson)
    {
        if (string.Equals(expectedJson, actualJson, StringComparison.Ordinal))
        {
            return true;
        }

        using var expected = JsonDocument.Parse(expectedJson);
        using var actual = JsonDocument.Parse(actualJson);
        return JsonElementEquals(expected.RootElement, actual.RootElement);
    }

    private static bool JsonElementEquals(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return false;
        }

        switch (left.ValueKind)
        {
            case JsonValueKind.Object:
                var rightProperties = right.EnumerateObject().ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal);
                foreach (var property in left.EnumerateObject())
                {
                    if (!rightProperties.TryGetValue(property.Name, out var rightValue) ||
                        !JsonElementEquals(property.Value, rightValue))
                    {
                        return false;
                    }
                }

                return left.EnumerateObject().Count() == rightProperties.Count;

            case JsonValueKind.Array:
                var leftItems = left.EnumerateArray().ToArray();
                var rightItems = right.EnumerateArray().ToArray();
                if (leftItems.Length != rightItems.Length)
                {
                    return false;
                }

                for (var i = 0; i < leftItems.Length; i++)
                {
                    if (!JsonElementEquals(leftItems[i], rightItems[i]))
                    {
                        return false;
                    }
                }

                return true;

            case JsonValueKind.String:
                return string.Equals(left.GetString(), right.GetString(), StringComparison.Ordinal);

            case JsonValueKind.Number:
                return left.GetRawText() == right.GetRawText();

            case JsonValueKind.True:
            case JsonValueKind.False:
                return left.GetBoolean() == right.GetBoolean();

            case JsonValueKind.Null:
                return true;

            default:
                return false;
        }
    }
}
