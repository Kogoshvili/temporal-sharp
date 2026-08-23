using Temporalio.Activities;
using Temporalio.Workflows;

namespace Kogoshvili.Temporal.SampleApp;

// Activity/activity-contract violations (TMP31xx, TMP3202, TMP3203).
public static class ActivityViolationActivities
{
    // TMP3101 — long-running activity that never heartbeats.
    [Activity]
    public static async Task LongRunningNoHeartbeat()
    {
        for (var i = 0; i < 10; i++)
        {
            await Task.Delay(1);
        }
    }

    // No heartbeat (used with HeartbeatTimeout below -> TMP3102).
    [Activity]
    public static Task Quick() => Task.CompletedTask;

    // TMP3104 — heartbeats but is not long-running.
    [Activity]
    public static Task QuickHeartbeat()
    {
        ActivityExecutionContext.Current.Heartbeat();
        return Task.CompletedTask;
    }

    // Long-running AND heartbeating (correct contract).
    [Activity]
    public static async Task LongRunningHeartbeat()
    {
        for (var i = 0; i < 10; i++)
        {
            ActivityExecutionContext.Current.Heartbeat();
            await Task.Delay(1);
        }
    }
}

[Workflow]
public class HeartbeatContractViolations
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        // TMP3102 — HeartbeatTimeout set but the activity never heartbeats.
        await Workflow.ExecuteActivityAsync(
            () => ActivityViolationActivities.Quick(),
            new ActivityOptions
            {
                StartToCloseTimeout = TimeSpan.FromMinutes(1),
                HeartbeatTimeout = TimeSpan.FromSeconds(10),
            });

        // TMP3103 — activity heartbeats but no HeartbeatTimeout is set.
        await Workflow.ExecuteActivityAsync(
            () => ActivityViolationActivities.LongRunningHeartbeat(),
            new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(1) });
    }
}

// TMP3203 — activity method mutates instance state (activities must be stateless).
public class StatefulActivity
{
    private int attempts;

    [Activity]
    public Task DoWork()
    {
        attempts++;
        return Task.CompletedTask;
    }
}

// TMP3202 — typed-lambda target is not marked [Activity].
[Workflow]
public class MissingActivityAttributeWorkflow
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        await Workflow.ExecuteActivityAsync(
            () => PlainHelper.DoWork(),
            new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(1) });
    }
}

public static class PlainHelper
{
    public static Task DoWork() => Task.CompletedTask;
}
