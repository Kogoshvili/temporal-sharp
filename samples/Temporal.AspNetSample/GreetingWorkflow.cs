using Temporalio.Activities;
using Temporalio.Workflows;

namespace Kogoshvili.Temporal.AspNetSample;

[Workflow]
public sealed class GreetingWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string name)
    {
        return await Workflow.ExecuteActivityAsync(
            () => GreetingActivities.GetGreeting(name),
            new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(1) });
    }
}

public static class GreetingActivities
{
    [Activity]
    public static Task<string> GetGreeting(string name) =>
        Task.FromResult($"Hello, {name}!");
}
