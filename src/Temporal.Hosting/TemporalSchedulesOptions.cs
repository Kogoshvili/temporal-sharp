namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// A single schedule's declarative definition, bound from
/// <c>Temporal:Schedules:&lt;id&gt;</c>. Mirrors the SDK's
/// <see cref="Temporalio.Client.Schedules.Schedule"/> shape directly: no custom
/// shorthand is introduced. Workflow arguments are intentionally omitted (they
/// are typed and code-only — see <see cref="IScheduleOps"/>); schedule memos and
/// search attributes are likewise code-only.
/// </summary>
public sealed class TemporalScheduleOptions
{
    /// <summary>Gets or sets the workflow-start action the schedule fires.</summary>
    public TemporalScheduleActionOptions? Action { get; set; }

    /// <summary>Gets or sets when the schedule fires.</summary>
    public TemporalScheduleSpecOptions? Spec { get; set; }

    /// <summary>Gets or sets overlap/failure policies.</summary>
    public TemporalSchedulePolicyOptions? Policy { get; set; }

    /// <summary>Gets or sets the schedule lifecycle state.</summary>
    public TemporalScheduleStateOptions? State { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to trigger one action immediately
    /// on creation. Create-time only, not persisted.
    /// </summary>
    public bool? TriggerImmediately { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether registration should reconcile an
    /// already-existing schedule toward this definition (describe + update on
    /// drift) rather than no-op. Defaults to <c>false</c> (pure get-or-create).
    /// </summary>
    public bool Reconcile { get; set; }
}

/// <summary>
/// The workflow-start action a schedule fires, bound from
/// <c>Temporal:Schedules:&lt;id&gt;:Action</c>. Maps onto the SDK's
/// <see cref="Temporalio.Client.Schedules.ScheduleActionStartWorkflow"/>. Only the
/// options the SDK allows on a scheduled start are exposed; <c>Id</c> and
/// <c>TaskQueue</c> are required.
/// </summary>
public sealed class TemporalScheduleActionOptions
{
    /// <summary>Gets or sets the workflow type name.</summary>
    public string? Workflow { get; set; }

    /// <summary>Gets or sets the task queue the workflow starts on.</summary>
    public string? TaskQueue { get; set; }

    /// <summary>
    /// Gets or sets the workflow-ID template. Supports the same
    /// <c>{Type}</c>/<c>{Type:s}</c>, <c>{Queue}</c>, and <c>{Guid}</c>
    /// placeholders as <see cref="WorkflowIdOptions"/>.
    /// </summary>
    public string? WorkflowId { get; set; }

    /// <summary>Gets or sets the timeout of a single workflow run.</summary>
    public TimeSpan? RunTimeout { get; set; }

    /// <summary>Gets or sets the timeout of a single workflow task.</summary>
    public TimeSpan? TaskTimeout { get; set; }

    /// <summary>Gets or sets the total execution timeout including retries and Continue-As-New.</summary>
    public TimeSpan? ExecutionTimeout { get; set; }

    /// <summary>Gets or sets the retry policy, or <c>null</c> to never retry (the SDK default).</summary>
    public RetryPolicyOptions? Retry { get; set; }

    /// <summary>Gets or sets the workflow's static summary.</summary>
    public string? StaticSummary { get; set; }

    /// <summary>Gets or sets the workflow's static details.</summary>
    public string? StaticDetails { get; set; }
}

/// <summary>
/// When the schedule fires, bound from
/// <c>Temporal:Schedules:&lt;id&gt;:Spec</c>. Maps onto the SDK's
/// <see cref="Temporalio.Client.Schedules.ScheduleSpec"/>.
/// </summary>
public sealed class TemporalScheduleSpecOptions
{
    /// <summary>Gets or sets the calendar-based times.</summary>
    public List<TemporalScheduleCalendarOptions>? Calendars { get; set; }

    /// <summary>Gets or sets the interval-based times.</summary>
    public List<TemporalScheduleIntervalOptions>? Intervals { get; set; }

    /// <summary>Gets or sets legacy cron expressions (converted to calendars server-side).</summary>
    public List<string>? Cron { get; set; }

    /// <summary>Gets or sets calendar times to skip.</summary>
    public List<TemporalScheduleCalendarOptions>? Skip { get; set; }

    /// <summary>Gets or sets the time before which matching times are skipped.</summary>
    public DateTime? StartAt { get; set; }

    /// <summary>Gets or sets the time after which matching times are skipped.</summary>
    public DateTime? EndAt { get; set; }

    /// <summary>Gets or sets the jitter to apply to each action.</summary>
    public TimeSpan? Jitter { get; set; }

    /// <summary>Gets or sets the IANA time zone name, e.g. <c>US/Central</c>.</summary>
    public string? TimeZoneName { get; set; }
}

/// <summary>
/// A calendar-based time spec, bound from
/// <c>Temporal:Schedules:&lt;id&gt;:Spec:Calendars</c>. Maps onto the SDK's
/// <see cref="Temporalio.Client.Schedules.ScheduleCalendarSpec"/>; each field is a
/// list of inclusive <see cref="ScheduleRangeOptions"/> ranges.
/// </summary>
public sealed class TemporalScheduleCalendarOptions
{
    /// <summary>Gets or sets the second ranges, 0-59. Defaults to <c>[0]</c>.</summary>
    public List<ScheduleRangeOptions>? Second { get; set; }

    /// <summary>Gets or sets the minute ranges, 0-59. Defaults to <c>[0]</c>.</summary>
    public List<ScheduleRangeOptions>? Minute { get; set; }

    /// <summary>Gets or sets the hour ranges, 0-23. Defaults to <c>[0]</c>.</summary>
    public List<ScheduleRangeOptions>? Hour { get; set; }

    /// <summary>Gets or sets the day-of-month ranges, 1-31. Defaults to all days.</summary>
    public List<ScheduleRangeOptions>? DayOfMonth { get; set; }

    /// <summary>Gets or sets the month ranges, 1-12. Defaults to all months.</summary>
    public List<ScheduleRangeOptions>? Month { get; set; }

    /// <summary>Gets or sets the year ranges. Defaults to all years.</summary>
    public List<ScheduleRangeOptions>? Year { get; set; }

    /// <summary>Gets or sets the day-of-week ranges, 0-6 (0 is Sunday). Defaults to all days.</summary>
    public List<ScheduleRangeOptions>? DayOfWeek { get; set; }

    /// <summary>Gets or sets an optional free-form comment.</summary>
    public string? Comment { get; set; }
}

/// <summary>
/// An inclusive range for a calendar field, bound from
/// <c>...:Calendars:&lt;field&gt;</c>. Maps onto the SDK's
/// <see cref="Temporalio.Client.Schedules.ScheduleRange"/>. <c>End</c> defaults to
/// <c>Start</c> and <c>Step</c> defaults to <c>1</c>.
/// </summary>
public sealed class ScheduleRangeOptions
{
    /// <summary>Gets or sets the inclusive start of the range.</summary>
    public int Start { get; set; }

    /// <summary>Gets or sets the inclusive end of the range. Defaults to <see cref="Start"/>.</summary>
    public int? End { get; set; }

    /// <summary>Gets or sets the step between values. Defaults to <c>1</c>.</summary>
    public int? Step { get; set; }
}

/// <summary>
/// An interval-based time spec, bound from
/// <c>Temporal:Schedules:&lt;id&gt;:Spec:Intervals</c>. Maps onto the SDK's
/// <see cref="Temporalio.Client.Schedules.ScheduleIntervalSpec"/>.
/// </summary>
public sealed class TemporalScheduleIntervalOptions
{
    /// <summary>Gets or sets the period to repeat.</summary>
    public TimeSpan? Every { get; set; }

    /// <summary>Gets or sets the fixed offset added to each period.</summary>
    public TimeSpan? Offset { get; set; }
}

/// <summary>
/// Overlap/failure policies, bound from
/// <c>Temporal:Schedules:&lt;id&gt;:Policy</c>. Maps onto the SDK's
/// <see cref="Temporalio.Client.Schedules.SchedulePolicy"/>.
/// </summary>
public sealed class TemporalSchedulePolicyOptions
{
    /// <summary>Gets or sets what happens when an action starts while another is still running. SDK default is <c>Skip</c>.</summary>
    public Temporalio.Api.Enums.V1.ScheduleOverlapPolicy? Overlap { get; set; }

    /// <summary>Gets or sets how far into the past to run missed actions after an outage. SDK default is 365 days.</summary>
    public TimeSpan? CatchupWindow { get; set; }

    /// <summary>Gets or sets whether to pause the schedule when an action fails or times out. SDK default is <c>false</c>.</summary>
    public bool? PauseOnFailure { get; set; }
}

/// <summary>
/// Lifecycle state, bound from <c>Temporal:Schedules:&lt;id&gt;:State</c>. Maps onto
/// the SDK's <see cref="Temporalio.Client.Schedules.ScheduleState"/>.
/// </summary>
public sealed class TemporalScheduleStateOptions
{
    /// <summary>Gets or sets a human-readable note.</summary>
    public string? Note { get; set; }

    /// <summary>Gets or sets whether the schedule is paused.</summary>
    public bool? Paused { get; set; }

    /// <summary>Gets or sets whether remaining actions decrement on each action taken.</summary>
    public bool? LimitedActions { get; set; }

    /// <summary>Gets or sets the actions remaining; once 0, no further actions fire.</summary>
    public long? RemainingActions { get; set; }
}
