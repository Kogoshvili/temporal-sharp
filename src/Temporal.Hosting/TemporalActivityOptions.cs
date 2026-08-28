using Temporalio.Workflows;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Activity-options presets, bound from <c>Temporal:ActivityOptions</c>. A single
/// preset maps to both a regular <see cref="Temporalio.Workflows.ActivityOptions"/>
/// and a <see cref="Temporalio.Workflows.LocalActivityOptions"/>; type-specific
/// fields apply only to the type that supports them. Workflows (which cannot use
/// dependency injection) resolve presets through <see cref="ActivityOptionsRegistry"/>,
/// which is seeded once at startup from this section.
/// </summary>
public sealed class TemporalActivityOptions
{
    /// <summary>Gets or sets the default preset for regular activities, returned by <see cref="ActivityOptionsRegistry.GetDefault"/>, or <c>null</c>.</summary>
    public ActivityOptionsPreset? Default { get; set; }

    /// <summary>Gets or sets the default preset for local activities, returned by <see cref="ActivityOptionsRegistry.GetLocalDefault"/>, or <c>null</c>.</summary>
    public ActivityOptionsPreset? LocalDefault { get; set; }

    /// <summary>Gets or sets named presets, keyed by name and resolved via <see cref="ActivityOptionsRegistry.Get"/> / <see cref="ActivityOptionsRegistry.GetLocal"/>.</summary>
    public Dictionary<string, ActivityOptionsPreset>? Presets { get; set; }
}

/// <summary>
/// A single activity-options preset, shared by regular and local activities.
/// Every property is nullable: <c>null</c> means "leave the SDK default
/// untouched". Either <see cref="ScheduleToCloseTimeout"/> or
/// <see cref="StartToCloseTimeout"/> must be set (the SDK's own rule). Duration
/// values are bound from configuration as time-span strings. The
/// <see cref="HeartbeatTimeout"/> and <see cref="TaskQueue"/> properties apply
/// only to regular activities; <see cref="LocalRetryThreshold"/> applies only to
/// local activities.
/// </summary>
public sealed class ActivityOptionsPreset
{
    /// <summary>Gets or sets the schedule-to-close timeout.</summary>
    public TimeSpan? ScheduleToCloseTimeout { get; set; }

    /// <summary>Gets or sets the schedule-to-start timeout.</summary>
    public TimeSpan? ScheduleToStartTimeout { get; set; }

    /// <summary>Gets or sets the start-to-close timeout.</summary>
    public TimeSpan? StartToCloseTimeout { get; set; }

    /// <summary>Gets or sets the heartbeat timeout (regular activities only).</summary>
    public TimeSpan? HeartbeatTimeout { get; set; }

    /// <summary>Gets or sets the cancellation type, or <c>null</c> for the SDK default.</summary>
    public ActivityCancellationType? CancellationType { get; set; }

    /// <summary>Gets or sets the task queue the activity runs on (regular activities only), or <c>null</c> for the workflow task queue.</summary>
    public string? TaskQueue { get; set; }

    /// <summary>Gets or sets the retry policy, or <c>null</c> to retry forever (the SDK default).</summary>
    public RetryPolicyOptions? Retry { get; set; }

    /// <summary>Gets or sets the local retry threshold (local activities only), or <c>null</c> for the SDK default.</summary>
    public TimeSpan? LocalRetryThreshold { get; set; }

    /// <summary>Gets or sets the activity ID, or <c>null</c> for the SDK default.</summary>
    public string? ActivityId { get; set; }

    /// <summary>Gets or sets a single-line summary for the activity, or <c>null</c>.</summary>
    public string? Summary { get; set; }
}
