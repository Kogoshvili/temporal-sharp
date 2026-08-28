using Temporalio.Client;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Maps configuration-bound <see cref="WorkflowOptionsPreset"/> values onto a
/// <see cref="WorkflowOptions"/>. Null preset properties leave the SDK defaults
/// (and any values already on the target) untouched, so presets can be layered
/// default-first, per-type-second.
/// </summary>
internal static class WorkflowOptionsFactory
{
    public static void Apply(WorkflowOptionsPreset? preset, WorkflowOptions options)
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

        if (preset.IdConflictPolicy is { } idConflictPolicy)
        {
            options.IdConflictPolicy = idConflictPolicy;
        }

        if (preset.StartDelay is { } startDelay)
        {
            options.StartDelay = startDelay;
        }

        if (preset.Retry is { } retry)
        {
            options.RetryPolicy = RetryPolicyFactory.Build(retry);
        }

        if (preset.TaskQueue is { } taskQueue)
        {
            options.TaskQueue = taskQueue;
        }
    }
}
