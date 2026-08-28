using System.Linq.Expressions;
using Temporalio.Client;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Typed workflow operations facade: a thin, config-aware wrapper over
/// <see cref="ITemporalClient"/> that starts, signals, queries, results,
/// terminates, cancels, restarts, and lists workflows. Workflow type is derived
/// from the generic argument, and the task queue / workflow ID / options are
/// resolved from <c>Temporal:Workflows</c> (see <see cref="WorkflowOptionsRegistry"/>),
/// with explicit per-call arguments always winning.
/// </summary>
public interface IWorkflowOps
{
    /// <summary>Starts a workflow via a lambda invoking its run method, returning a typed-result handle.</summary>
    Task<WorkflowHandle<TWorkflow, TResult>> StartAsync<TWorkflow, TResult>(
        Expression<Func<TWorkflow, Task<TResult>>> runCall,
        string? taskQueue = null,
        string? workflowId = null,
        Action<WorkflowOptions>? configure = null);

    /// <summary>Starts a workflow via a lambda invoking its run method, returning a typed handle.</summary>
    Task<WorkflowHandle<TWorkflow>> StartAsync<TWorkflow>(
        Expression<Func<TWorkflow, Task>> runCall,
        string? taskQueue = null,
        string? workflowId = null,
        Action<WorkflowOptions>? configure = null);

    /// <summary>
    /// Starts a workflow whose run method takes a single <typeparamref name="TParams"/>
    /// argument, passing that argument directly. Prefer the lambda overloads when
    /// the run method takes multiple parameters.
    /// </summary>
    Task<WorkflowHandle<TWorkflow>> StartAsync<TWorkflow, TParams>(
        TParams args,
        string? taskQueue = null,
        string? workflowId = null,
        Action<WorkflowOptions>? configure = null);

    /// <summary>
    /// Starts a workflow whose run method takes a single <typeparamref name="TParams"/>
    /// argument, passing it directly and returning a typed-result handle.
    /// </summary>
    Task<WorkflowHandle<TWorkflow, TResult>> StartAsync<TWorkflow, TParams, TResult>(
        TParams args,
        string? taskQueue = null,
        string? workflowId = null,
        Action<WorkflowOptions>? configure = null);

    /// <summary>
    /// Starts a workflow whose run method takes no arguments, returning a
    /// typed-result handle.
    /// </summary>
    Task<WorkflowHandle<TWorkflow, TResult>> StartAsync<TWorkflow, TResult>(
        string? taskQueue = null,
        string? workflowId = null,
        Action<WorkflowOptions>? configure = null);

    /// <summary>
    /// Starts a workflow whose run method takes no arguments, returning a typed
    /// handle.
    /// </summary>
    Task<WorkflowHandle<TWorkflow>> StartAsync<TWorkflow>(
        string? taskQueue = null,
        string? workflowId = null,
        Action<WorkflowOptions>? configure = null);

    /// <summary>Starts a workflow by type name and run arguments, returning an untyped handle.</summary>
    Task<WorkflowHandle> StartAsync(
        string workflow,
        IReadOnlyCollection<object?> args,
        string? taskQueue = null,
        string? workflowId = null,
        Action<WorkflowOptions>? configure = null);

    /// <summary>Gets a typed handle for an existing workflow.</summary>
    WorkflowHandle<TWorkflow> Handle<TWorkflow>(string workflowId, string? runId = null);

    /// <summary>Signals a workflow via a lambda invoking a signal method.</summary>
    Task SignalAsync<TWorkflow>(
        string workflowId,
        Expression<Func<TWorkflow, Task>> signalCall,
        string? runId = null);

    /// <summary>Signals a workflow by signal name and arguments.</summary>
    Task SignalAsync(
        string workflowId,
        string signal,
        IReadOnlyCollection<object?> args,
        string? runId = null);

    /// <summary>Queries a workflow via a lambda invoking a query method or property.</summary>
    Task<TQueryResult> QueryAsync<TWorkflow, TQueryResult>(
        string workflowId,
        Expression<Func<TWorkflow, TQueryResult>> queryCall,
        string? runId = null);

    /// <summary>Queries a workflow by query name and arguments.</summary>
    Task<TQueryResult> QueryAsync<TQueryResult>(
        string workflowId,
        string query,
        IReadOnlyCollection<object?> args,
        string? runId = null);

    /// <summary>Gets the result of a workflow, deserialized into <typeparamref name="TResult"/>.</summary>
    Task<TResult> ResultAsync<TResult>(
        string workflowId,
        string? runId = null,
        bool followRuns = true);

    /// <summary>Terminates a workflow.</summary>
    Task TerminateAsync(string workflowId, string? reason = null, string? runId = null);

    /// <summary>Cancels a workflow.</summary>
    Task CancelAsync(string workflowId, string? runId = null);

    /// <summary>Restarts a workflow: terminates the current run (best-effort), then starts a fresh run with a new ID.</summary>
    Task<WorkflowHandle<TWorkflow, TResult>> RestartAsync<TWorkflow, TResult>(
        string workflowId,
        Expression<Func<TWorkflow, Task<TResult>>> runCall,
        string? taskQueue = null,
        Action<WorkflowOptions>? configure = null);

    /// <summary>Restarts a workflow: terminates the current run (best-effort), then starts a fresh run with a new ID.</summary>
    Task<WorkflowHandle<TWorkflow>> RestartAsync<TWorkflow>(
        string workflowId,
        Expression<Func<TWorkflow, Task>> runCall,
        string? taskQueue = null,
        Action<WorkflowOptions>? configure = null);

    /// <summary>Restarts a workflow by type name: terminates the current run (best-effort), then starts a fresh run with a new ID.</summary>
    Task<WorkflowHandle> RestartAsync(
        string workflowId,
        string workflow,
        IReadOnlyCollection<object?> args,
        string? taskQueue = null,
        Action<WorkflowOptions>? configure = null);

    /// <summary>Lists workflows matching the given visibility query.</summary>
    IAsyncEnumerable<WorkflowExecution> ListAsync(string query, WorkflowListOptions? options = null);
}
