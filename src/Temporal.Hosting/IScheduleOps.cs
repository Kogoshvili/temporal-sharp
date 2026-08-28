using System.Linq.Expressions;
using Temporalio.Client;
using Temporalio.Client.Schedules;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Schedule operations facade over <see cref="ITemporalClient"/>. The
/// <see cref="RegisterAsync(string, Schedule, ScheduleOptions?, bool)"/> method is
/// the idempotent core: it creates the schedule if absent and, with
/// <c>reconcile: true</c>, drives an already-existing schedule toward the desired
/// definition. The remaining members are thin pass-throughs to
/// <see cref="ScheduleHandle"/> / <see cref="ITemporalClient"/>.
/// </summary>
public interface IScheduleOps
{
    /// <summary>
    /// Registers a schedule idempotently. Creates it if absent. When
    /// <paramref name="reconcile"/> is <c>true</c> and the schedule already exists,
    /// updates it toward <paramref name="schedule"/>; otherwise it is left
    /// untouched.
    /// </summary>
    Task<ScheduleHandle> RegisterAsync(
        string scheduleId,
        Schedule schedule,
        ScheduleOptions? options = null,
        bool reconcile = false);

    /// <summary>Registers a schedule from a typed workflow invocation and an explicit spec.</summary>
    Task<ScheduleHandle> RegisterAsync<TWorkflow, TResult>(
        string scheduleId,
        Expression<Func<TWorkflow, Task<TResult>>> runCall,
        WorkflowOptions workflowOptions,
        ScheduleSpec spec,
        SchedulePolicy? policy = null,
        ScheduleState? state = null,
        ScheduleOptions? options = null,
        bool reconcile = false);

    /// <summary>Registers a schedule from a typed workflow invocation (no result) and an explicit spec.</summary>
    Task<ScheduleHandle> RegisterAsync<TWorkflow>(
        string scheduleId,
        Expression<Func<TWorkflow, Task>> runCall,
        WorkflowOptions workflowOptions,
        ScheduleSpec spec,
        SchedulePolicy? policy = null,
        ScheduleState? state = null,
        ScheduleOptions? options = null,
        bool reconcile = false);

    /// <summary>Registers a schedule from a workflow type name and args.</summary>
    Task<ScheduleHandle> RegisterAsync(
        string scheduleId,
        string workflow,
        IReadOnlyCollection<object?> args,
        WorkflowOptions workflowOptions,
        ScheduleSpec spec,
        SchedulePolicy? policy = null,
        ScheduleState? state = null,
        ScheduleOptions? options = null,
        bool reconcile = false);

    /// <summary>Gets a handle to a schedule by ID.</summary>
    ScheduleHandle GetHandle(string scheduleId);

    /// <summary>Describes a schedule.</summary>
    Task<ScheduleDescription> DescribeAsync(string scheduleId, RpcOptions? rpcOptions = null);

    /// <summary>Deletes a schedule.</summary>
    Task DeleteAsync(string scheduleId, RpcOptions? rpcOptions = null);

    /// <summary>Pauses a schedule.</summary>
    Task PauseAsync(string scheduleId, string? note = null, RpcOptions? rpcOptions = null);

    /// <summary>Unpauses a schedule.</summary>
    Task UnpauseAsync(string scheduleId, string? note = null, RpcOptions? rpcOptions = null);

    /// <summary>Triggers a schedule to fire immediately.</summary>
    Task TriggerAsync(string scheduleId, ScheduleTriggerOptions? options = null);

    /// <summary>Backfills a schedule over the given periods.</summary>
    Task BackfillAsync(
        string scheduleId,
        IReadOnlyCollection<ScheduleBackfill> backfills,
        RpcOptions? rpcOptions = null);

    /// <summary>Updates a schedule via a callback.</summary>
    Task UpdateAsync(
        string scheduleId,
        Func<ScheduleUpdateInput, ScheduleUpdate?> updater,
        RpcOptions? rpcOptions = null);

    /// <summary>Lists schedules.</summary>
    IAsyncEnumerable<ScheduleListDescription> ListAsync(ScheduleListOptions? options = null);
}
