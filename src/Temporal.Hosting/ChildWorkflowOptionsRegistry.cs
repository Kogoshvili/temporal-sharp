using Temporalio.Workflows;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Static access point for child-workflow options resolved from the
/// <c>Temporal:Workflows</c> configuration (the <c>Default</c> preset and
/// per-type <c>ByType</c> overrides, plus the child ID convention). Any workflow
/// can be started as a child, so child workflows resolve the same
/// <c>Workflows</c> config as client starts. Workflows cannot use dependency
/// injection (they run in the deterministic replay sandbox), so this is seeded
/// once by the starter at <c>AddTemporal</c> time, mirroring
/// <see cref="ActivityOptionsRegistry"/>.
/// </summary>
public static class ChildWorkflowOptionsRegistry
{
    private static readonly object Sync = new();

    private static WorkflowOptionsPreset? defaultPreset;
    private static IReadOnlyDictionary<string, WorkflowOptionsPreset> byTypePresets =
        new Dictionary<string, WorkflowOptionsPreset>(StringComparer.Ordinal);
    private static string? childIdFormat;

    /// <summary>
    /// Builds fresh <see cref="ChildWorkflowOptions"/> for the given workflow
    /// type by layering the <c>Default</c> preset then the <c>ByType</c> override.
    /// Returns a new object each call, so callers may mutate it safely.
    /// </summary>
    public static ChildWorkflowOptions Resolve(string workflowType)
    {
        ArgumentException.ThrowIfNullOrEmpty(workflowType);

        var options = ChildWorkflowOptionsFactory.Build(defaultPreset);

        if (byTypePresets.TryGetValue(workflowType, out var preset))
        {
            ChildWorkflowOptionsFactory.Apply(preset, options);
        }

        return options;
    }

    /// <summary>
    /// Resolves the configured child-ID template: the shipped
    /// <see cref="WorkflowIdOptions.DefaultChildFormat"/> when unset,
    /// <c>null</c> when configured to an empty string (opt out), or the
    /// configured template otherwise.
    /// </summary>
    public static string? ResolveChildIdFormat() => childIdFormat switch
    {
        null => WorkflowIdOptions.DefaultChildFormat,
        "" => null,
        _ => childIdFormat,
    };

    /// <summary>
    /// Replaces the registry contents. Called once by the starter during
    /// registration; the registry is read-only during workflow execution.
    /// </summary>
    internal static void Replace(
        WorkflowOptionsPreset? defaultPreset,
        IReadOnlyDictionary<string, WorkflowOptionsPreset>? byTypePresets,
        string? childIdFormat)
    {
        lock (Sync)
        {
            ChildWorkflowOptionsRegistry.defaultPreset = defaultPreset;
            ChildWorkflowOptionsRegistry.byTypePresets = byTypePresets is null
                ? new Dictionary<string, WorkflowOptionsPreset>(StringComparer.Ordinal)
                : new Dictionary<string, WorkflowOptionsPreset>(byTypePresets, StringComparer.Ordinal);
            ChildWorkflowOptionsRegistry.childIdFormat = childIdFormat;
        }
    }
}
