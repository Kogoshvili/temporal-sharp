using Temporalio.Activities;
using Temporalio.Workflows;

namespace Kogoshvili.Temporal.ConsoleWorker;

[Workflow]
public sealed class GreetingWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string name)
    {
        return await Workflow.ExecuteActivityAsync(
            () => GreetingActivities.Greet(name),
            new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(1) });
    }
}

public static class GreetingActivities
{
    [Activity]
    public static Task<string> Greet(string name) =>
        Task.FromResult($"Hello from the console worker, {name}!");
}
