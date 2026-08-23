using Temporalio.Activities;
using Temporalio.Workflows;

namespace Kogoshvili.Temporal.SampleApp;

// This workflow uses the deterministic SDK APIs, so the analyzer stays silent.
[Workflow]
public class GoodWorkflow
{
    private bool approved;

    [WorkflowRun]
    public async Task RunAsync()
    {
        // Deterministic time, identity, and delays.
        var now = Workflow.UtcNow;
        var id = Workflow.NewGuid();
        await Workflow.DelayAsync(100);

        // Replay-aware logging.
        Workflow.Logger.LogInformation("started");

        // Activity with required timeouts, via a typed lambda. The activity
        // heartbeats, so it also sets a HeartbeatTimeout.
        var opts = new ActivityOptions
        {
            StartToCloseTimeout = TimeSpan.FromMinutes(1),
            HeartbeatTimeout = TimeSpan.FromSeconds(30),
        };
        await Workflow.ExecuteActivityAsync(() => GoodActivities.Greet(), opts);

        // Wait on a condition with a timeout, and handle the result.
        var met = await Workflow.WaitConditionAsync(() => approved, TimeSpan.FromMinutes(5));
        if (!met)
        {
            Workflow.Logger.LogWarning("approval timed out");
        }

        // Pinned culture for parsing workflow state.
        _ = long.Parse("42", System.Globalization.CultureInfo.InvariantCulture);
    }
}

public static class GoodActivities
{
    // Stateless, long-running activity that heartbeats.
    [Activity]
    public static async Task Greet()
    {
        for (var i = 0; i < 10; i++)
        {
            ActivityExecutionContext.Current.Heartbeat();
            await Task.Delay(1);
        }
    }
}
