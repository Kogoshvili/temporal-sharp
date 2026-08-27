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
