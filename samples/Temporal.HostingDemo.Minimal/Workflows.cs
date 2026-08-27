using Temporalio.Activities;
using Temporalio.Workflows;

namespace Kogoshvili.Temporal.HostingDemo.Minimal;

[Workflow]
public sealed class GreetingWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string name) =>
        await Workflow.ExecuteActivityAsync(
            () => GreetingActivities.Greet(name),
            new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(1) });
}

public static class GreetingActivities
{
    [Activity]
    public static string Greet(string name) => $"Hello from Kogoshvili.Temporal.Hosting, {name}!";
}
