using Temporalio.Workflows;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Maps a configuration-bound <see cref="ActivityOptionsPreset"/> onto the SDK's
/// <see cref="LocalActivityOptions"/>. Null preset properties leave the SDK
/// defaults untouched, and regular-activity-only fields (heartbeat, task queue)
/// are ignored.
/// </summary>
internal static class LocalActivityOptionsFactory
{
    public static LocalActivityOptions? Build(ActivityOptionsPreset? preset)
    {
        if (preset is null)
        {
            return null;
        }

        var options = new LocalActivityOptions();

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

        if (preset.CancellationType is { } cancellationType)
        {
            options.CancellationType = cancellationType;
        }

        if (preset.LocalRetryThreshold is { } localRetryThreshold)
        {
            options.LocalRetryThreshold = localRetryThreshold;
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
