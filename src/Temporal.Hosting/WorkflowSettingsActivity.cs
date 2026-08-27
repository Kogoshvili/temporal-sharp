using System.Collections;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Temporalio.Activities;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Built-in local activity that reads a workflow's settings from
/// <c>Temporal:WorkflowSettings</c>. It is registered on every worker by the
/// starter and invoked by the <see cref="WorkflowSettings"/> facade.
/// </summary>
/// <remarks>
/// The activity returns the settings serialized as JSON. Because a local
/// activity's result is recorded in workflow history, a workflow reads a stable
/// snapshot even when the configuration is live-reloaded mid-run.
/// </remarks>
public sealed class WorkflowSettingsActivity
{
    private readonly IOptionsMonitor<TemporalOptions> options;

    /// <summary>Initializes a new instance of the <see cref="WorkflowSettingsActivity"/> class.</summary>
    public WorkflowSettingsActivity(IOptionsMonitor<TemporalOptions> options)
    {
        this.options = options;
    }

    /// <summary>
    /// Reads and serializes the settings for the given workflow type, merging
    /// <c>ByType</c> over <c>Default</c>.
    /// </summary>
    [Activity]
    public string Read(string workflowType)
    {
        var settings = options.CurrentValue.WorkflowSettings;

        var merged = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (settings?.Default is { } defaults)
        {
            foreach (var (key, value) in defaults)
            {
                merged[key] = value;
            }
        }

        if (settings?.ByType is { } byType && byType.TryGetValue(workflowType, out var specific))
        {
            foreach (var (key, value) in specific)
            {
                merged[key] = value;
            }
        }

        return JsonSerializer.Serialize(Convert(merged));
    }

    // Configuration values are always strings, so convert them to their natural
    // types (bool / integer / floating-point) before serializing, so a typed
    // TSettings deserializes without manual string parsing.
    private static object? Convert(object? value) => value switch
    {
        string s => ConvertScalar(s),
        IDictionary<string, object?> d => d.ToDictionary(kv => kv.Key, kv => Convert(kv.Value), StringComparer.Ordinal),
        IEnumerable e => e.Cast<object?>().Select(Convert).ToList(),
        _ => value,
    };

    private static object ConvertScalar(string value)
    {
        if (bool.TryParse(value, out var boolean))
        {
            return boolean;
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
        {
            return integer;
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return number;
        }

        return value;
    }
}
