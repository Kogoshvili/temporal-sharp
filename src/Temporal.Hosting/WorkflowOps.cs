using System.Linq.Expressions;
using Temporalio.Client;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Kogoshvili.Temporal.Hosting;

/// <inheritdoc cref="IWorkflowOps" />
public sealed class WorkflowOps : IWorkflowOps
{
    private readonly ITemporalClient client;
    private readonly WorkflowOptionsRegistry registry;

    /// <summary>Initializes a new instance of the <see cref="WorkflowOps"/> class.</summary>
    public WorkflowOps(ITemporalClient client, WorkflowOptionsRegistry registry)
    {
        this.client = client;
        this.registry = registry;
    }

    /// <inheritdoc />
    public async Task<WorkflowHandle<TWorkflow, TResult>> StartAsync<TWorkflow, TResult>(
        Expression<Func<TWorkflow, Task<TResult>>> runCall,
        string? taskQueue = null,
        string? workflowId = null,
        Action<WorkflowOptions>? configure = null)
    {
        var options = BuildOptions(WorkflowName<TWorkflow>(), taskQueue, workflowId, configure);
        return await client.StartWorkflowAsync(runCall, options).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<WorkflowHandle<TWorkflow>> StartAsync<TWorkflow>(
        Expression<Func<TWorkflow, Task>> runCall,
        string? taskQueue = null,
        string? workflowId = null,
        Action<WorkflowOptions>? configure = null)
    {
        var options = BuildOptions(WorkflowName<TWorkflow>(), taskQueue, workflowId, configure);
        return await client.StartWorkflowAsync(runCall, options).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<WorkflowHandle<TWorkflow>> StartAsync<TWorkflow, TParams>(
        TParams args,
        string? taskQueue = null,
        string? workflowId = null,
        Action<WorkflowOptions>? configure = null)
    {
        var name = WorkflowName<TWorkflow>();
        var options = BuildOptions(name, taskQueue, workflowId, configure);

        var handle = await client.StartWorkflowAsync(
            name, new object?[] { args }, options).ConfigureAwait(false);

        return new WorkflowHandle<TWorkflow>(
            handle.Client, handle.Id, handle.RunId, handle.ResultRunId, handle.FirstExecutionRunId);
    }

    /// <inheritdoc />
    public async Task<WorkflowHandle> StartAsync(
        string workflow,
        IReadOnlyCollection<object?> args,
        string? taskQueue = null,
        string? workflowId = null,
        Action<WorkflowOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(workflow);
        var options = BuildOptions(workflow, taskQueue, workflowId, configure);
        return await client.StartWorkflowAsync(workflow, args, options).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public WorkflowHandle<TWorkflow> Handle<TWorkflow>(string workflowId, string? runId = null) =>
        client.GetWorkflowHandle<TWorkflow>(workflowId, runId);

    /// <inheritdoc />
    public Task SignalAsync<TWorkflow>(
        string workflowId,
        Expression<Func<TWorkflow, Task>> signalCall,
        string? runId = null) =>
        client.GetWorkflowHandle<TWorkflow>(workflowId, runId).SignalAsync(signalCall);

    /// <inheritdoc />
    public Task SignalAsync(
        string workflowId,
        string signal,
        IReadOnlyCollection<object?> args,
        string? runId = null) =>
        client.GetWorkflowHandle(workflowId, runId).SignalAsync(signal, args);

    /// <inheritdoc />
    public Task<TQueryResult> QueryAsync<TWorkflow, TQueryResult>(
        string workflowId,
        Expression<Func<TWorkflow, TQueryResult>> queryCall,
        string? runId = null) =>
        client.GetWorkflowHandle<TWorkflow>(workflowId, runId).QueryAsync(queryCall);

    /// <inheritdoc />
    public Task<TQueryResult> QueryAsync<TQueryResult>(
        string workflowId,
        string query,
        IReadOnlyCollection<object?> args,
        string? runId = null) =>
        client.GetWorkflowHandle(workflowId, runId).QueryAsync<TQueryResult>(query, args);

    /// <inheritdoc />
    public Task<TResult> ResultAsync<TResult>(
        string workflowId,
        string? runId = null,
        bool followRuns = true) =>
        client.GetWorkflowHandle(workflowId, runId).GetResultAsync<TResult>(followRuns);

    /// <inheritdoc />
    public Task TerminateAsync(string workflowId, string? reason = null, string? runId = null) =>
        client.GetWorkflowHandle(workflowId, runId).TerminateAsync(reason);

    /// <inheritdoc />
    public Task CancelAsync(string workflowId, string? runId = null) =>
        client.GetWorkflowHandle(workflowId, runId).CancelAsync();

    /// <inheritdoc />
    public async Task<WorkflowHandle<TWorkflow, TResult>> RestartAsync<TWorkflow, TResult>(
        string workflowId,
        Expression<Func<TWorkflow, Task<TResult>>> runCall,
        string? taskQueue = null,
        Action<WorkflowOptions>? configure = null)
    {
        await TerminateBestEffortAsync(workflowId).ConfigureAwait(false);
        return await StartAsync(runCall, taskQueue, configure: configure).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<WorkflowHandle<TWorkflow>> RestartAsync<TWorkflow>(
        string workflowId,
        Expression<Func<TWorkflow, Task>> runCall,
        string? taskQueue = null,
        Action<WorkflowOptions>? configure = null)
    {
        await TerminateBestEffortAsync(workflowId).ConfigureAwait(false);
        return await StartAsync(runCall, taskQueue, configure: configure).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<WorkflowHandle> RestartAsync(
        string workflowId,
        string workflow,
        IReadOnlyCollection<object?> args,
        string? taskQueue = null,
        Action<WorkflowOptions>? configure = null)
    {
        await TerminateBestEffortAsync(workflowId).ConfigureAwait(false);
        return await StartAsync(workflow, args, taskQueue, configure: configure).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<WorkflowExecution> ListAsync(string query, WorkflowListOptions? options = null) =>
        client.ListWorkflowsAsync(query, options);

    private WorkflowOptions BuildOptions(
        string workflowType,
        string? taskQueue,
        string? workflowId,
        Action<WorkflowOptions>? configure) =>
        registry.Build(workflowType, taskQueue, workflowId, configure);

    internal static string WorkflowName<TWorkflow>()
    {
        var definition = WorkflowDefinition.Create(typeof(TWorkflow));
        return definition.Name ?? throw new ArgumentException(
            $"Workflow type '{typeof(TWorkflow).Name}' is dynamic and has no name; " +
            "start it via the string overload instead.");
    }

    private async Task TerminateBestEffortAsync(string workflowId)
    {
        try
        {
            await client.GetWorkflowHandle(workflowId).TerminateAsync().ConfigureAwait(false);
        }
        catch (RpcException ex) when (ex.Code == RpcException.StatusCode.NotFound)
        {
        }
    }
}
