using Temporalio.Client;
using Temporalio.Client.Schedules;
using Temporalio.Common;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Maps configuration-bound <see cref="TemporalScheduleOptions"/> onto the SDK's
/// <see cref="Schedule"/> and create-time <see cref="ScheduleOptions"/>. Mirrors
/// the SDK object model directly; no shorthand parsing is applied.
/// </summary>
internal static class ScheduleFactory
{
    public static Schedule BuildSchedule(TemporalScheduleOptions config, string scheduleId)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrEmpty(scheduleId);

        return new Schedule(
            Action: BuildAction(config.Action, scheduleId),
            Spec: BuildSpec(config.Spec))
        {
            Policy = BuildPolicy(config.Policy),
            State = BuildState(config.State),
        };
    }

    public static ScheduleOptions BuildScheduleOptions(TemporalScheduleOptions config)
    {
        var options = new ScheduleOptions();
        if (config.TriggerImmediately is { } triggerImmediately)
        {
            options.TriggerImmediately = triggerImmediately;
        }

        return options;
    }

    private static ScheduleActionStartWorkflow BuildAction(
        TemporalScheduleActionOptions? action,
        string scheduleId)
    {
        if (action is null)
        {
            throw new ArgumentException($"Schedule '{scheduleId}' requires an Action.", nameof(action));
        }

        if (string.IsNullOrWhiteSpace(action.Workflow))
        {
            throw new ArgumentException($"Schedule '{scheduleId}' requires an Action:Workflow.", nameof(action));
        }

        if (string.IsNullOrWhiteSpace(action.TaskQueue))
        {
            throw new ArgumentException($"Schedule '{scheduleId}' requires an Action:TaskQueue.", nameof(action));
        }

        if (string.IsNullOrWhiteSpace(action.WorkflowId))
        {
            throw new ArgumentException($"Schedule '{scheduleId}' requires an Action:WorkflowId.", nameof(action));
        }

        var workflowId = WorkflowIdFormatter.Format(action.WorkflowId, action.Workflow, action.TaskQueue);

        var options = new WorkflowOptions(id: workflowId, taskQueue: action.TaskQueue)
        {
            RunTimeout = action.RunTimeout,
            TaskTimeout = action.TaskTimeout,
            ExecutionTimeout = action.ExecutionTimeout,
            RetryPolicy = action.Retry is { } retry ? RetryPolicyFactory.Build(retry) : null,
            StaticSummary = action.StaticSummary,
            StaticDetails = action.StaticDetails,
        };

        return new ScheduleActionStartWorkflow(
            action.Workflow,
            Array.Empty<object?>(),
            options);
    }

    private static ScheduleSpec BuildSpec(TemporalScheduleSpecOptions? spec)
    {
        if (spec is null)
        {
            return new ScheduleSpec();
        }

        return new ScheduleSpec
        {
            Calendars = ToCalendars(spec.Calendars),
            Intervals = ToIntervals(spec.Intervals),
            CronExpressions = spec.Cron ?? new List<string>(),
            Skip = ToCalendars(spec.Skip),
            StartAt = spec.StartAt,
            EndAt = spec.EndAt,
            Jitter = spec.Jitter,
            TimeZoneName = spec.TimeZoneName,
        };
    }

    private static IReadOnlyCollection<ScheduleCalendarSpec> ToCalendars(
        List<TemporalScheduleCalendarOptions>? calendars) =>
        calendars?.Select(BuildCalendar).ToList()
            ?? new List<ScheduleCalendarSpec>();

    private static IReadOnlyCollection<ScheduleIntervalSpec> ToIntervals(
        List<TemporalScheduleIntervalOptions>? intervals) =>
        intervals?.Select(BuildInterval).ToList()
            ?? new List<ScheduleIntervalSpec>();

    private static ScheduleCalendarSpec BuildCalendar(TemporalScheduleCalendarOptions calendar) =>
        new()
        {
            Second = BuildRanges(calendar.Second, ScheduleCalendarSpec.Beginning),
            Minute = BuildRanges(calendar.Minute, ScheduleCalendarSpec.Beginning),
            Hour = BuildRanges(calendar.Hour, ScheduleCalendarSpec.Beginning),
            DayOfMonth = BuildRanges(calendar.DayOfMonth, ScheduleCalendarSpec.AllMonthDays),
            Month = BuildRanges(calendar.Month, ScheduleCalendarSpec.AllMonths),
            Year = BuildRanges(calendar.Year, Array.Empty<ScheduleRange>()),
            DayOfWeek = BuildRanges(calendar.DayOfWeek, ScheduleCalendarSpec.AllWeekDays),
            Comment = calendar.Comment,
        };

    private static IReadOnlyCollection<ScheduleRange> BuildRanges(
        List<ScheduleRangeOptions>? ranges,
        IReadOnlyCollection<ScheduleRange> fallback)
    {
        if (ranges is null || ranges.Count == 0)
        {
            return fallback;
        }

        return ranges.Select(r => r.End is { } end
            ? new ScheduleRange(r.Start, end, r.Step ?? 1)
            : new ScheduleRange(r.Start)).ToList();
    }

    private static ScheduleIntervalSpec BuildInterval(TemporalScheduleIntervalOptions interval)
    {
        if (interval.Every is not { } every)
        {
            throw new ArgumentException("A schedule interval requires an Every value.", nameof(interval));
        }

        return new ScheduleIntervalSpec(every, interval.Offset);
    }

    private static SchedulePolicy BuildPolicy(TemporalSchedulePolicyOptions? policy) =>
        new()
        {
            Overlap = policy?.Overlap ?? Temporalio.Api.Enums.V1.ScheduleOverlapPolicy.Skip,
            CatchupWindow = policy?.CatchupWindow ?? TimeSpan.FromDays(365),
            PauseOnFailure = policy?.PauseOnFailure ?? false,
        };

    private static ScheduleState BuildState(TemporalScheduleStateOptions? state) =>
        new()
        {
            Note = state?.Note,
            Paused = state?.Paused ?? false,
            LimitedActions = state?.LimitedActions ?? false,
            RemainingActions = state?.RemainingActions ?? 0,
        };
}
