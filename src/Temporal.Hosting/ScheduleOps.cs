using System.Linq.Expressions;
using Temporalio.Client;
using Temporalio.Client.Schedules;
using Temporalio.Exceptions;

namespace Kogoshvili.Temporal.Hosting;

/// <inheritdoc cref="IScheduleOps" />
public sealed class ScheduleOps : IScheduleOps
{
    private readonly ITemporalClient client;

    /// <summary>Initializes a new instance of the <see cref="ScheduleOps"/> class.</summary>
    public ScheduleOps(ITemporalClient client)
    {
        this.client = client;
    }

    /// <inheritdoc />
    public async Task<ScheduleHandle> RegisterAsync(
        string scheduleId,
        Schedule schedule,
        ScheduleOptions? options = null,
        bool reconcile = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(scheduleId);
        ArgumentNullException.ThrowIfNull(schedule);

        try
        {
            return await client.CreateScheduleAsync(scheduleId, schedule, options).ConfigureAwait(false);
        }
        catch (ScheduleAlreadyRunningException)
        {
            var handle = client.GetScheduleHandle(scheduleId);
            if (reconcile)
            {
                await handle.UpdateAsync(
                    input => input.Description.Schedule == schedule ? null : new ScheduleUpdate(schedule))
                    .ConfigureAwait(false);
            }

            return handle;
        }
    }

    /// <inheritdoc />
    public Task<ScheduleHandle> RegisterAsync<TWorkflow, TResult>(
        string scheduleId,
        Expression<Func<TWorkflow, Task<TResult>>> runCall,
        WorkflowOptions workflowOptions,
        ScheduleSpec spec,
        SchedulePolicy? policy = null,
        ScheduleState? state = null,
        ScheduleOptions? options = null,
        bool reconcile = false) =>
        RegisterAsync(
            scheduleId,
            BuildSchedule(ScheduleActionStartWorkflow.Create(runCall, workflowOptions), spec, policy, state),
            options,
            reconcile);

    /// <inheritdoc />
    public Task<ScheduleHandle> RegisterAsync<TWorkflow>(
        string scheduleId,
        Expression<Func<TWorkflow, Task>> runCall,
        WorkflowOptions workflowOptions,
        ScheduleSpec spec,
        SchedulePolicy? policy = null,
        ScheduleState? state = null,
        ScheduleOptions? options = null,
        bool reconcile = false) =>
        RegisterAsync(
            scheduleId,
            BuildSchedule(ScheduleActionStartWorkflow.Create(runCall, workflowOptions), spec, policy, state),
            options,
            reconcile);

    /// <inheritdoc />
    public Task<ScheduleHandle> RegisterAsync(
        string scheduleId,
        string workflow,
        IReadOnlyCollection<object?> args,
        WorkflowOptions workflowOptions,
        ScheduleSpec spec,
        SchedulePolicy? policy = null,
        ScheduleState? state = null,
        ScheduleOptions? options = null,
        bool reconcile = false) =>
        RegisterAsync(
            scheduleId,
            BuildSchedule(ScheduleActionStartWorkflow.Create(workflow, args, workflowOptions), spec, policy, state),
            options,
            reconcile);

    /// <inheritdoc />
    public ScheduleHandle GetHandle(string scheduleId) => client.GetScheduleHandle(scheduleId);

    /// <inheritdoc />
    public Task<ScheduleDescription> DescribeAsync(string scheduleId, RpcOptions? rpcOptions = null) =>
        client.GetScheduleHandle(scheduleId).DescribeAsync(rpcOptions);

    /// <inheritdoc />
    public Task DeleteAsync(string scheduleId, RpcOptions? rpcOptions = null) =>
        client.GetScheduleHandle(scheduleId).DeleteAsync(rpcOptions);

    /// <inheritdoc />
    public Task PauseAsync(string scheduleId, string? note = null, RpcOptions? rpcOptions = null) =>
        client.GetScheduleHandle(scheduleId).PauseAsync(note, rpcOptions);

    /// <inheritdoc />
    public Task UnpauseAsync(string scheduleId, string? note = null, RpcOptions? rpcOptions = null) =>
        client.GetScheduleHandle(scheduleId).UnpauseAsync(note, rpcOptions);

    /// <inheritdoc />
    public Task TriggerAsync(string scheduleId, ScheduleTriggerOptions? options = null) =>
        client.GetScheduleHandle(scheduleId).TriggerAsync(options);

    /// <inheritdoc />
    public Task BackfillAsync(
        string scheduleId,
        IReadOnlyCollection<ScheduleBackfill> backfills,
        RpcOptions? rpcOptions = null) =>
        client.GetScheduleHandle(scheduleId).BackfillAsync(backfills, rpcOptions);

    /// <inheritdoc />
    public Task UpdateAsync(
        string scheduleId,
        Func<ScheduleUpdateInput, ScheduleUpdate?> updater,
        RpcOptions? rpcOptions = null) =>
        client.GetScheduleHandle(scheduleId).UpdateAsync(updater, rpcOptions);

    /// <inheritdoc />
    public IAsyncEnumerable<ScheduleListDescription> ListAsync(ScheduleListOptions? options = null) =>
        client.ListSchedulesAsync(options);

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
