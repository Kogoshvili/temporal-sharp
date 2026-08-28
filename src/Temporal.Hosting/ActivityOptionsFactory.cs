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

        if (preset.ActivityId is { } activityId)
        {
            options.ActivityId = activityId;
        }

        if (preset.Summary is { } summary)
        {
            options.Summary = summary;
        }

        if (preset.Retry is { } retry)
        {
            options.RetryPolicy = RetryPolicyFactory.Build(retry);
        }

        return options;
    }
}
