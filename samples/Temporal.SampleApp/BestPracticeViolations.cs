using Temporalio.Activities;
using Temporalio.Workflows;

namespace Kogoshvili.Temporal.SampleApp;

// Showcases the best-practice rules (TMP41xx/TMP42xx).
[Workflow]
public class BestPracticeViolations
{
    // TMP4101 — multiple positional parameters instead of a single object.
    [WorkflowRun]
    public async Task RunAsync(string orderId, int amount, string customerId)
    {
        // TMP4105 — hard-coded task-queue name.
        await Workflow.ExecuteActivityAsync(
            () => BestPracticeActivities.Fetch(),
            new ActivityOptions
            {
                StartToCloseTimeout = TimeSpan.FromMinutes(1),
                TaskQueue = "orders",
            });

        // TMP4107 — local activity performing blocking I/O (Task.Delay).
        await Workflow.ExecuteLocalActivityAsync(
            () => BestPracticeActivities.Blocking(),
            new LocalActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(1) });

        // TMP4106 — consecutive local activities with no intervening command.
        await Workflow.ExecuteLocalActivityAsync(
            () => BestPracticeActivities.First(),
            new LocalActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(1) });
        await Workflow.ExecuteLocalActivityAsync(
            () => BestPracticeActivities.Second(),
            new LocalActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(1) });

        // TMP4104 — loop with no await (CPU-heavy work belongs in an activity).
        for (var i = 0; i < 100; i++)
        {
            _ = orderId.Length * amount;
        }
    }
}

public static class BestPracticeActivities
{
    [Activity]
    public static Task Fetch() => Task.CompletedTask;

    [Activity]
    public static Task First() => Task.CompletedTask;

    [Activity]
    public static Task Second() => Task.CompletedTask;

    [Activity]
    public static async Task Blocking()
    {
        await Task.Delay(100);
    }
}
