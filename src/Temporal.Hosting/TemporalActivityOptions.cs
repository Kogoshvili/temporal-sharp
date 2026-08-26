using Temporalio.Workflows;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Named <see cref="Temporalio.Workflows.ActivityOptions"/> presets, bound from
/// <c>Temporal:ActivityOptions</c>. Workflows (which cannot use dependency
/// injection) resolve presets through <see cref="ActivityOptionsRegistry"/>,
/// which is seeded once at startup from this section.
/// </summary>
public sealed class TemporalActivityOptions
{
    /// <summary>Gets or sets the default preset returned by <see cref="ActivityOptionsRegistry.GetDefault"/>, or <c>null</c>.</summary>
    public ActivityOptionsPreset? Default { get; set; }

    /// <summary>Gets or sets named presets, keyed by name and resolved via <see cref="ActivityOptionsRegistry.Get"/>.</summary>
    public Dictionary<string, ActivityOptionsPreset>? Presets { get; set; }
}

/// <summary>
/// A single activity-options preset. Every property is nullable: <c>null</c>
/// means "leave the SDK default untouched". Either
/// <see cref="ScheduleToCloseTimeout"/> or <see cref="StartToCloseTimeout"/>
/// must be set (the SDK's own rule). Duration values are bound from
/// configuration as time-span strings.
/// </summary>
public sealed class ActivityOptionsPreset
{
    /// <summary>Gets or sets the schedule-to-close timeout.</summary>
    public TimeSpan? ScheduleToCloseTimeout { get; set; }

    /// <summary>Gets or sets the schedule-to-start timeout.</summary>
    public TimeSpan? ScheduleToStartTimeout { get; set; }

    /// <summary>Gets or sets the start-to-close timeout.</summary>
    public TimeSpan? StartToCloseTimeout { get; set; }

    /// <summary>Gets or sets the heartbeat timeout.</summary>
    public TimeSpan? HeartbeatTimeout { get; set; }

    /// <summary>Gets or sets the cancellation type, or <c>null</c> for the SDK default.</summary>
    public ActivityCancellationType? CancellationType { get; set; }

    /// <summary>Gets or sets the task queue the activity runs on, or <c>null</c> for the workflow task queue.</summary>
    public string? TaskQueue { get; set; }

    /// <summary>Gets or sets the retry policy, or <c>null</c> to retry forever (the SDK default).</summary>
    public ActivityRetryPolicyOptions? Retry { get; set; }
}

/// <summary>
/// Retry-policy options for an <see cref="ActivityOptionsPreset"/>, mirroring
/// the SDK's <c>RetryPolicy</c>. Every property is nullable: <c>null</c> means
/// "leave the SDK default untouched".
/// </summary>
public sealed class ActivityRetryPolicyOptions
{
    /// <summary>Gets or sets the backoff interval for the first retry. SDK default is 1s.</summary>
    public TimeSpan? InitialInterval { get; set; }

    /// <summary>Gets or sets the backoff multiplier. SDK default is 2.0.</summary>
    public float? BackoffCoefficient { get; set; }

    /// <summary>Gets or sets the maximum backoff interval, or <c>null</c> for none.</summary>
    public TimeSpan? MaximumInterval { get; set; }

    /// <summary>Gets or sets the maximum number of attempts, or <c>0</c> for unlimited. SDK default is 0.</summary>
    public int? MaximumAttempts { get; set; }

    /// <summary>Gets or sets error types that must not be retried.</summary>
    public IReadOnlyCollection<string>? NonRetryableErrorTypes { get; set; }
}
