namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Workflow start/execution configuration, bound from
/// <c>Temporal:Workflows</c>. Defines a default preset, per-workflow-type
/// overrides, and workflow-ID conventions. Callers resolve these into a
/// <see cref="Temporalio.Client.WorkflowOptions"/> via
/// <see cref="WorkflowOptionsRegistry"/>, merging them with their own per-call
/// values (explicit call arguments always win).
/// </summary>
public sealed class TemporalWorkflowOptions
{
    /// <summary>Gets or sets workflow-ID conventions.</summary>
    public WorkflowIdOptions? Id { get; set; }

    /// <summary>Gets or sets the default preset applied to every started workflow.</summary>
    public WorkflowOptionsPreset? Default { get; set; }

    /// <summary>
    /// Gets or sets per-workflow-type overrides, keyed by workflow type name
    /// (e.g. <c>"MoneyTransferWorkflow"</c>) and applied after
    /// <see cref="Default"/>.
    /// </summary>
    public Dictionary<string, WorkflowOptionsPreset>? ByType { get; set; }
}

/// <summary>
/// A single workflow-options preset. Every property is nullable: <c>null</c>
/// means "leave the SDK default untouched". Duration values are bound from
/// configuration as time-span strings.
/// </summary>
public sealed class WorkflowOptionsPreset
{
    /// <summary>Gets or sets the timeout of a single workflow run.</summary>
    public TimeSpan? RunTimeout { get; set; }

    /// <summary>Gets or sets the timeout of a single workflow task.</summary>
    public TimeSpan? TaskTimeout { get; set; }

    /// <summary>Gets or sets the total execution timeout including retries and Continue-As-New.</summary>
    public TimeSpan? ExecutionTimeout { get; set; }

    /// <summary>Gets or sets how already-existing workflows of the same ID are treated.</summary>
    public Temporalio.Api.Enums.V1.WorkflowIdConflictPolicy? IdConflictPolicy { get; set; }

    /// <summary>Gets or sets the amount of time to wait before starting the workflow.</summary>
    public TimeSpan? StartDelay { get; set; }

    /// <summary>Gets or sets the retry policy, or <c>null</c> to never retry (the SDK default).</summary>
    public RetryPolicyOptions? Retry { get; set; }
}

/// <summary>
/// Workflow-ID conventions, bound from <c>Temporal:Workflows:Id</c>. The
/// <see cref="Format"/> template supports the <c>{Type}</c>, <c>{Queue}</c>,
/// and <c>{Guid}</c> (or <c>{Guid:N}</c>/<c>{Guid:D}</c>/<c>{Guid:B}</c>)
/// placeholders. When no format is set and no explicit ID is supplied, the SDK
/// generates a random UUID.
/// </summary>
public sealed class WorkflowIdOptions
{
    /// <summary>
    /// Gets or sets the workflow-ID format template, e.g. <c>"{Type}-{Guid:N}"</c>.
    /// </summary>
    public string? Format { get; set; }
}
