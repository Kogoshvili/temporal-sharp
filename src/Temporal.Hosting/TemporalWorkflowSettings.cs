namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Workflow-level settings bound from <c>Temporal:WorkflowSettings</c>. Unlike
/// <see cref="TemporalWorkflowOptions"/> (start options supplied by the caller),
/// these are the worker's own configuration that workflows read for themselves
/// via <see cref="WorkflowSettings"/>. Values are arbitrary JSON objects,
/// keyed by workflow type name, merged over an optional <see cref="Default"/>.
/// </summary>
public sealed class TemporalWorkflowSettings
{
    /// <summary>
    /// Gets or sets the default settings, applied to every workflow type and
    /// overridden per-key by a matching <see cref="ByType"/> entry.
    /// </summary>
    public Dictionary<string, object?>? Default { get; set; }

    /// <summary>
    /// Gets or sets per-workflow-type settings, keyed by workflow type name
    /// (e.g. <c>"MyWorkflow"</c>) and merged over <see cref="Default"/>.
    /// </summary>
    public Dictionary<string, Dictionary<string, object?>>? ByType { get; set; }
}
