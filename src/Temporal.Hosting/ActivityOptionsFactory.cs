using Temporalio.Common;
using Temporalio.Workflows;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Maps configuration-bound <see cref="ActivityOptionsPreset"/> values onto the
/// SDK's <see cref="Temporalio.Workflows.ActivityOptions"/>. Null preset
/// properties leave the SDK defaults untouched.
/// </summary>
internal static class ActivityOptionsFactory
{
    public static ActivityOptions? Build(ActivityOptionsPreset? preset)
    {
        if (preset is null)
        {
            return null;
        }

        var options = new ActivityOptions();

        if (preset.ScheduleToCloseTimeout is { } scheduleToClose)
        {
            options.ScheduleToCloseTimeout = scheduleToClose;
        }

        if (preset.ScheduleToStartTimeout is { } scheduleToStart)
        {
            options.ScheduleToStartTimeout = scheduleToStart;
        }

        if (preset.StartToCloseTimeout is { } startToClose)
        {
            options.StartToCloseTimeout = startToClose;
        }

        if (preset.HeartbeatTimeout is { } heartbeat)
        {
            options.HeartbeatTimeout = heartbeat;
        }

        if (preset.CancellationType is { } cancellationType)
        {
            options.CancellationType = cancellationType;
        }

        if (preset.TaskQueue is { } taskQueue)
        {
            options.TaskQueue = taskQueue;
        }

        if (preset.Retry is { } retry)
        {
            options.RetryPolicy = BuildRetry(retry);
        }

        return options;
    }

    private static RetryPolicy BuildRetry(ActivityRetryPolicyOptions retry)
    {
        var policy = new RetryPolicy();

        if (retry.InitialInterval is { } initialInterval)
        {
            policy.InitialInterval = initialInterval;
        }

        if (retry.BackoffCoefficient is { } backoffCoefficient)
        {
            policy.BackoffCoefficient = backoffCoefficient;
        }

        if (retry.MaximumInterval is { } maximumInterval)
        {
            policy.MaximumInterval = maximumInterval;
        }

        if (retry.MaximumAttempts is { } maximumAttempts)
        {
            policy.MaximumAttempts = maximumAttempts;
        }

        if (retry.NonRetryableErrorTypes is { } nonRetryable)
        {
            policy.NonRetryableErrorTypes = nonRetryable;
        }

        return policy;
    }
}
