using System.Linq.Expressions;
using Temporalio.Workflows;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Workflow-side facade for executing activities via activity-options presets.
/// Mirrors <c>Workflow.ExecuteActivityAsync</c> / <c>Workflow.ExecuteLocalActivityAsync</c>
/// but resolves <c>ActivityOptions</c> from the configured presets: pass a preset
/// name, omit it to use the default preset, or pass an explicit
/// <see cref="ActivityOptions"/> / <see cref="LocalActivityOptions"/> object for
/// full control with no reduction in the underlying SDK's surface.
/// </summary>
public static class ActivityOps
{
    /// <summary>Executes a static non-async activity with result, using the default preset.</summary>
    public static Task<TResult> ExecuteAsync<TResult>(Expression<Func<TResult>> activityCall, string? preset = null) =>
        Workflow.ExecuteActivityAsync(activityCall, ActivityOptionsRegistry.Resolve(preset));

    /// <summary>Executes a static non-async activity with result, using explicit options.</summary>
    public static Task<TResult> ExecuteAsync<TResult>(Expression<Func<TResult>> activityCall, ActivityOptions options) =>
        Workflow.ExecuteActivityAsync(activityCall, options);

    /// <summary>Executes a static non-async activity without result, using the default preset.</summary>
    public static Task ExecuteAsync(Expression<Action> activityCall, string? preset = null) =>
        Workflow.ExecuteActivityAsync(activityCall, ActivityOptionsRegistry.Resolve(preset));

    /// <summary>Executes a static non-async activity without result, using explicit options.</summary>
    public static Task ExecuteAsync(Expression<Action> activityCall, ActivityOptions options) =>
        Workflow.ExecuteActivityAsync(activityCall, options);

    /// <summary>Executes a non-static non-async activity with result, using the default preset.</summary>
    public static Task<TResult> ExecuteAsync<TActivity, TResult>(Expression<Func<TActivity, TResult>> activityCall, string? preset = null) =>
        Workflow.ExecuteActivityAsync(activityCall, ActivityOptionsRegistry.Resolve(preset));

    /// <summary>Executes a non-static non-async activity with result, using explicit options.</summary>
    public static Task<TResult> ExecuteAsync<TActivity, TResult>(Expression<Func<TActivity, TResult>> activityCall, ActivityOptions options) =>
        Workflow.ExecuteActivityAsync(activityCall, options);

    /// <summary>Executes a non-static non-async activity without result, using the default preset.</summary>
    public static Task ExecuteAsync<TActivity>(Expression<Action<TActivity>> activityCall, string? preset = null) =>
        Workflow.ExecuteActivityAsync(activityCall, ActivityOptionsRegistry.Resolve(preset));

    /// <summary>Executes a non-static non-async activity without result, using explicit options.</summary>
    public static Task ExecuteAsync<TActivity>(Expression<Action<TActivity>> activityCall, ActivityOptions options) =>
        Workflow.ExecuteActivityAsync(activityCall, options);

    /// <summary>Executes a static async activity with result, using the default preset.</summary>
    public static Task<TResult> ExecuteAsync<TResult>(Expression<Func<Task<TResult>>> activityCall, string? preset = null) =>
        Workflow.ExecuteActivityAsync(activityCall, ActivityOptionsRegistry.Resolve(preset));

    /// <summary>Executes a static async activity with result, using explicit options.</summary>
    public static Task<TResult> ExecuteAsync<TResult>(Expression<Func<Task<TResult>>> activityCall, ActivityOptions options) =>
        Workflow.ExecuteActivityAsync(activityCall, options);

    /// <summary>Executes a static async activity without result, using the default preset.</summary>
    public static Task ExecuteAsync(Expression<Func<Task>> activityCall, string? preset = null) =>
        Workflow.ExecuteActivityAsync(activityCall, ActivityOptionsRegistry.Resolve(preset));

    /// <summary>Executes a static async activity without result, using explicit options.</summary>
    public static Task ExecuteAsync(Expression<Func<Task>> activityCall, ActivityOptions options) =>
        Workflow.ExecuteActivityAsync(activityCall, options);

    /// <summary>Executes a non-static async activity with result, using the default preset.</summary>
    public static Task<TResult> ExecuteAsync<TActivity, TResult>(Expression<Func<TActivity, Task<TResult>>> activityCall, string? preset = null) =>
        Workflow.ExecuteActivityAsync(activityCall, ActivityOptionsRegistry.Resolve(preset));

    /// <summary>Executes a non-static async activity with result, using explicit options.</summary>
    public static Task<TResult> ExecuteAsync<TActivity, TResult>(Expression<Func<TActivity, Task<TResult>>> activityCall, ActivityOptions options) =>
        Workflow.ExecuteActivityAsync(activityCall, options);

    /// <summary>Executes a non-static async activity without result, using the default preset.</summary>
    public static Task ExecuteAsync<TActivity>(Expression<Func<TActivity, Task>> activityCall, string? preset = null) =>
        Workflow.ExecuteActivityAsync(activityCall, ActivityOptionsRegistry.Resolve(preset));

    /// <summary>Executes a non-static async activity without result, using explicit options.</summary>
    public static Task ExecuteAsync<TActivity>(Expression<Func<TActivity, Task>> activityCall, ActivityOptions options) =>
        Workflow.ExecuteActivityAsync(activityCall, options);

    /// <summary>Executes a static non-async local activity with result, using the default preset.</summary>
    public static Task<TResult> ExecuteLocalAsync<TResult>(Expression<Func<TResult>> activityCall, string? preset = null) =>
        Workflow.ExecuteLocalActivityAsync(activityCall, ActivityOptionsRegistry.ResolveLocal(preset));

    /// <summary>Executes a static non-async local activity with result, using explicit options.</summary>
    public static Task<TResult> ExecuteLocalAsync<TResult>(Expression<Func<TResult>> activityCall, LocalActivityOptions options) =>
        Workflow.ExecuteLocalActivityAsync(activityCall, options);

    /// <summary>Executes a static non-async local activity without result, using the default preset.</summary>
    public static Task ExecuteLocalAsync(Expression<Action> activityCall, string? preset = null) =>
        Workflow.ExecuteLocalActivityAsync(activityCall, ActivityOptionsRegistry.ResolveLocal(preset));

    /// <summary>Executes a static non-async local activity without result, using explicit options.</summary>
    public static Task ExecuteLocalAsync(Expression<Action> activityCall, LocalActivityOptions options) =>
        Workflow.ExecuteLocalActivityAsync(activityCall, options);

    /// <summary>Executes a non-static non-async local activity with result, using the default preset.</summary>
    public static Task<TResult> ExecuteLocalAsync<TActivity, TResult>(Expression<Func<TActivity, TResult>> activityCall, string? preset = null) =>
        Workflow.ExecuteLocalActivityAsync(activityCall, ActivityOptionsRegistry.ResolveLocal(preset));

    /// <summary>Executes a non-static non-async local activity with result, using explicit options.</summary>
    public static Task<TResult> ExecuteLocalAsync<TActivity, TResult>(Expression<Func<TActivity, TResult>> activityCall, LocalActivityOptions options) =>
        Workflow.ExecuteLocalActivityAsync(activityCall, options);

    /// <summary>Executes a non-static non-async local activity without result, using the default preset.</summary>
    public static Task ExecuteLocalAsync<TActivity>(Expression<Action<TActivity>> activityCall, string? preset = null) =>
        Workflow.ExecuteLocalActivityAsync(activityCall, ActivityOptionsRegistry.ResolveLocal(preset));

    /// <summary>Executes a non-static non-async local activity without result, using explicit options.</summary>
    public static Task ExecuteLocalAsync<TActivity>(Expression<Action<TActivity>> activityCall, LocalActivityOptions options) =>
        Workflow.ExecuteLocalActivityAsync(activityCall, options);

    /// <summary>Executes a static async local activity with result, using the default preset.</summary>
    public static Task<TResult> ExecuteLocalAsync<TResult>(Expression<Func<Task<TResult>>> activityCall, string? preset = null) =>
        Workflow.ExecuteLocalActivityAsync(activityCall, ActivityOptionsRegistry.ResolveLocal(preset));

    /// <summary>Executes a static async local activity with result, using explicit options.</summary>
    public static Task<TResult> ExecuteLocalAsync<TResult>(Expression<Func<Task<TResult>>> activityCall, LocalActivityOptions options) =>
        Workflow.ExecuteLocalActivityAsync(activityCall, options);

    /// <summary>Executes a static async local activity without result, using the default preset.</summary>
    public static Task ExecuteLocalAsync(Expression<Func<Task>> activityCall, string? preset = null) =>
        Workflow.ExecuteLocalActivityAsync(activityCall, ActivityOptionsRegistry.ResolveLocal(preset));

    /// <summary>Executes a static async local activity without result, using explicit options.</summary>
    public static Task ExecuteLocalAsync(Expression<Func<Task>> activityCall, LocalActivityOptions options) =>
        Workflow.ExecuteLocalActivityAsync(activityCall, options);

    /// <summary>Executes a non-static async local activity with result, using the default preset.</summary>
    public static Task<TResult> ExecuteLocalAsync<TActivity, TResult>(Expression<Func<TActivity, Task<TResult>>> activityCall, string? preset = null) =>
        Workflow.ExecuteLocalActivityAsync(activityCall, ActivityOptionsRegistry.ResolveLocal(preset));

    /// <summary>Executes a non-static async local activity with result, using explicit options.</summary>
    public static Task<TResult> ExecuteLocalAsync<TActivity, TResult>(Expression<Func<TActivity, Task<TResult>>> activityCall, LocalActivityOptions options) =>
        Workflow.ExecuteLocalActivityAsync(activityCall, options);

    /// <summary>Executes a non-static async local activity without result, using the default preset.</summary>
    public static Task ExecuteLocalAsync<TActivity>(Expression<Func<TActivity, Task>> activityCall, string? preset = null) =>
        Workflow.ExecuteLocalActivityAsync(activityCall, ActivityOptionsRegistry.ResolveLocal(preset));

    /// <summary>Executes a non-static async local activity without result, using explicit options.</summary>
    public static Task ExecuteLocalAsync<TActivity>(Expression<Func<TActivity, Task>> activityCall, LocalActivityOptions options) =>
        Workflow.ExecuteLocalActivityAsync(activityCall, options);
}
