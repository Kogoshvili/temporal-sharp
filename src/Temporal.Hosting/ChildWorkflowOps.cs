using System.Linq.Expressions;
using Temporalio.Workflows;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Workflow-side facade for executing child workflows via the
/// <c>Temporal:Workflows</c> configuration. Mirrors
/// <c>Workflow.ExecuteChildWorkflowAsync</c> / <c>Workflow.StartChildWorkflowAsync</c>
/// but resolves <see cref="ChildWorkflowOptions"/> from the workflow's
/// <c>Default</c> + <c>ByType</c> presets (any workflow can run as a child) and
/// applies the child workflow-ID convention. Pass an explicit
/// <see cref="ChildWorkflowOptions"/> to override a single call.
/// </summary>
public static class ChildWorkflowOps
{
    /// <summary>Executes a child workflow with a result, resolved from config.</summary>
    public static Task<TResult> ExecuteAsync<TWorkflow, TResult>(
        Expression<Func<TWorkflow, Task<TResult>>> runCall, ChildWorkflowOptions? options = null) =>
        Workflow.ExecuteChildWorkflowAsync(runCall, Resolve(WorkflowOps.WorkflowName<TWorkflow>(), options));

    /// <summary>Executes a child workflow without a result, resolved from config.</summary>
    public static Task ExecuteAsync<TWorkflow>(
        Expression<Func<TWorkflow, Task>> runCall, ChildWorkflowOptions? options = null) =>
        Workflow.ExecuteChildWorkflowAsync(runCall, Resolve(WorkflowOps.WorkflowName<TWorkflow>(), options));

    /// <summary>Executes a child workflow by name with a result, resolved from config.</summary>
    public static Task<TResult> ExecuteAsync<TResult>(
        string workflow, IReadOnlyCollection<object?> args, ChildWorkflowOptions? options = null) =>
        Workflow.ExecuteChildWorkflowAsync<TResult>(workflow, args, Resolve(workflow, options));

    /// <summary>Executes a child workflow by name without a result, resolved from config.</summary>
    public static Task ExecuteAsync(
        string workflow, IReadOnlyCollection<object?> args, ChildWorkflowOptions? options = null) =>
        Workflow.ExecuteChildWorkflowAsync(workflow, args, Resolve(workflow, options));

    /// <summary>Starts a child workflow with a result, returning its typed handle.</summary>
    public static Task<ChildWorkflowHandle<TWorkflow, TResult>> StartAsync<TWorkflow, TResult>(
        Expression<Func<TWorkflow, Task<TResult>>> runCall, ChildWorkflowOptions? options = null) =>
        Workflow.StartChildWorkflowAsync(runCall, Resolve(WorkflowOps.WorkflowName<TWorkflow>(), options));

    /// <summary>Starts a child workflow without a result, returning its handle.</summary>
    public static Task<ChildWorkflowHandle<TWorkflow>> StartAsync<TWorkflow>(
        Expression<Func<TWorkflow, Task>> runCall, ChildWorkflowOptions? options = null) =>
        Workflow.StartChildWorkflowAsync(runCall, Resolve(WorkflowOps.WorkflowName<TWorkflow>(), options));

    /// <summary>Starts a child workflow by name, returning its handle.</summary>
    public static Task<ChildWorkflowHandle> StartAsync(
        string workflow, IReadOnlyCollection<object?> args, ChildWorkflowOptions? options = null) =>
        Workflow.StartChildWorkflowAsync(workflow, args, Resolve(workflow, options));

    /// <summary>
    /// Executes a child workflow whose run method takes a single
    /// <typeparamref name="TParams"/> argument, passing it directly.
    /// </summary>
    public static Task ExecuteAsync<TWorkflow, TParams>(
        TParams args, ChildWorkflowOptions? options = null)
    {
        var name = WorkflowOps.WorkflowName<TWorkflow>();
        return Workflow.ExecuteChildWorkflowAsync(name, new object?[] { args }, Resolve(name, options));
    }

    /// <summary>
    /// Executes a child workflow whose run method takes a single
    /// <typeparamref name="TParams"/> argument, passing it directly and returning
    /// a typed result.
    /// </summary>
    public static Task<TResult> ExecuteAsync<TWorkflow, TParams, TResult>(
        TParams args, ChildWorkflowOptions? options = null)
    {
        var name = WorkflowOps.WorkflowName<TWorkflow>();
        return Workflow.ExecuteChildWorkflowAsync<TResult>(name, new object?[] { args }, Resolve(name, options));
    }

    /// <summary>
    /// Starts a child workflow whose run method takes a single
    /// <typeparamref name="TParams"/> argument, passing it directly and returning
    /// its handle.
    /// </summary>
    public static Task<ChildWorkflowHandle> StartAsync<TWorkflow, TParams>(
        TParams args, ChildWorkflowOptions? options = null)
    {
        var name = WorkflowOps.WorkflowName<TWorkflow>();
        return Workflow.StartChildWorkflowAsync(name, new object?[] { args }, Resolve(name, options));
    }

    /// <summary>
    /// Executes a child workflow whose run method takes no arguments, returning
    /// a typed result.
    /// </summary>
    public static Task<TResult> ExecuteAsync<TWorkflow, TResult>(
        ChildWorkflowOptions? options = null)
    {
        var name = WorkflowOps.WorkflowName<TWorkflow>();
        return Workflow.ExecuteChildWorkflowAsync<TResult>(name, Array.Empty<object?>(), Resolve(name, options));
    }

    /// <summary>
    /// Executes a child workflow whose run method takes no arguments.
    /// </summary>
    public static Task ExecuteAsync<TWorkflow>(
        ChildWorkflowOptions? options = null)
    {
        var name = WorkflowOps.WorkflowName<TWorkflow>();
        return Workflow.ExecuteChildWorkflowAsync(name, Array.Empty<object?>(), Resolve(name, options));
    }

    /// <summary>
    /// Starts a child workflow whose run method takes no arguments, returning
    /// its handle.
    /// </summary>
    public static Task<ChildWorkflowHandle> StartAsync<TWorkflow>(
        ChildWorkflowOptions? options = null)
    {
        var name = WorkflowOps.WorkflowName<TWorkflow>();
        return Workflow.StartChildWorkflowAsync(name, Array.Empty<object?>(), Resolve(name, options));
    }

    private static ChildWorkflowOptions Resolve(string workflowType, ChildWorkflowOptions? options)
    {
        var resolved = options is null
            ? ChildWorkflowOptionsRegistry.Resolve(workflowType)
            : (ChildWorkflowOptions)options.Clone();

        resolved.Id ??= BuildChildId(workflowType);
        return resolved;
    }

    private static string? BuildChildId(string workflowType)
    {
        var format = ChildWorkflowOptionsRegistry.ResolveChildIdFormat();
        if (format is null)
        {
            return null;
        }

        return WorkflowIdFormatter.Format(format, workflowType, parentId: Workflow.Info.WorkflowId);
    }
}
