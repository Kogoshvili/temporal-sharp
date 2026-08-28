using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Client;
using Temporalio.Client.Schedules;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Builder extensions for declaring schedules to be registered idempotently at
/// startup, in code rather than via <c>Temporal:Schedules</c>. Because these
/// accept a fully-built <see cref="Schedule"/> (or a typed workflow invocation),
/// they support typed workflow arguments — the one thing config-driven schedules
/// cannot express.
/// </summary>
public static class TemporalScheduleRegistrationExtensions
{
    /// <summary>Declares a schedule from a fully-built schedule definition.</summary>
    public static TemporalBuilder AddTemporalSchedule(
        this TemporalBuilder builder,
        string scheduleId,
        Schedule schedule,
        ScheduleOptions? options = null,
        bool reconcile = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(scheduleId);
        ArgumentNullException.ThrowIfNull(schedule);

        builder.Services.AddSingleton(new TemporalScheduleRegistration(scheduleId, schedule, options, reconcile));
        return builder;
    }

    /// <summary>Declares a schedule from a typed workflow invocation and an explicit spec.</summary>
    public static TemporalBuilder AddTemporalSchedule<TWorkflow, TResult>(
        this TemporalBuilder builder,
        string scheduleId,
        Expression<Func<TWorkflow, Task<TResult>>> runCall,
        WorkflowOptions workflowOptions,
        ScheduleSpec spec,
        SchedulePolicy? policy = null,
        ScheduleState? state = null,
        ScheduleOptions? options = null,
        bool reconcile = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(runCall);
        ArgumentNullException.ThrowIfNull(workflowOptions);
        ArgumentNullException.ThrowIfNull(spec);

        return builder.AddTemporalSchedule(
            scheduleId,
            BuildSchedule(ScheduleActionStartWorkflow.Create(runCall, workflowOptions), spec, policy, state),
            options,
            reconcile);
    }

    /// <summary>Declares a schedule from a typed workflow invocation (no result) and an explicit spec.</summary>
    public static TemporalBuilder AddTemporalSchedule<TWorkflow>(
        this TemporalBuilder builder,
        string scheduleId,
        Expression<Func<TWorkflow, Task>> runCall,
        WorkflowOptions workflowOptions,
        ScheduleSpec spec,
        SchedulePolicy? policy = null,
        ScheduleState? state = null,
        ScheduleOptions? options = null,
        bool reconcile = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(runCall);
        ArgumentNullException.ThrowIfNull(workflowOptions);
        ArgumentNullException.ThrowIfNull(spec);

        return builder.AddTemporalSchedule(
            scheduleId,
            BuildSchedule(ScheduleActionStartWorkflow.Create(runCall, workflowOptions), spec, policy, state),
            options,
            reconcile);
    }

    /// <summary>Declares a schedule from a workflow type name and args.</summary>
    public static TemporalBuilder AddTemporalSchedule(
        this TemporalBuilder builder,
        string scheduleId,
        string workflow,
        IReadOnlyCollection<object?> args,
        WorkflowOptions workflowOptions,
        ScheduleSpec spec,
        SchedulePolicy? policy = null,
        ScheduleState? state = null,
        ScheduleOptions? options = null,
        bool reconcile = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(workflowOptions);
        ArgumentNullException.ThrowIfNull(spec);

        return builder.AddTemporalSchedule(
            scheduleId,
            BuildSchedule(ScheduleActionStartWorkflow.Create(workflow, args, workflowOptions), spec, policy, state),
            options,
            reconcile);
    }

    private static Schedule BuildSchedule(
        ScheduleActionStartWorkflow action,
        ScheduleSpec spec,
        SchedulePolicy? policy,
        ScheduleState? state) =>
        new(action, spec)
        {
            Policy = policy ?? new SchedulePolicy(),
            State = state ?? new ScheduleState(),
        };
}
