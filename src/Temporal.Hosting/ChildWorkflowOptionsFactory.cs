using Temporalio.Workflows;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Maps configuration-bound <see cref="WorkflowOptionsPreset"/> values onto the
/// SDK's <see cref="ChildWorkflowOptions"/>. Null preset properties leave the
/// SDK defaults untouched, so presets can be layered default-first,
/// per-type-second. Client-only preset fields (e.g. <c>StartDelay</c>,
/// <c>IdConflictPolicy</c>) are ignored.
/// </summary>
internal static class ChildWorkflowOptionsFactory
{
    public static ChildWorkflowOptions Build(WorkflowOptionsPreset? preset)
    {
        var options = new ChildWorkflowOptions();
        Apply(preset, options);
        return options;
    }

    public static void Apply(WorkflowOptionsPreset? preset, ChildWorkflowOptions options)
    {
        if (preset is null)
        {
            return;
        }

        if (preset.RunTimeout is { } runTimeout)
        {
            options.RunTimeout = runTimeout;
        }

        if (preset.TaskTimeout is { } taskTimeout)
        {
            options.TaskTimeout = taskTimeout;
        }

        if (preset.ExecutionTimeout is { } executionTimeout)
        {
            options.ExecutionTimeout = executionTimeout;
        }

        if (preset.Retry is { } retry)
        {
            options.RetryPolicy = RetryPolicyFactory.Build(retry);
        }

        if (preset.TaskQueue is { } taskQueue)
        {
            options.TaskQueue = taskQueue;
        }

        if (preset.ParentClosePolicy is { } parentClosePolicy)
        {
            options.ParentClosePolicy = parentClosePolicy;
        }

        if (preset.CancellationType is { } cancellationType)
        {
            options.CancellationType = cancellationType;
        }
    }
}
