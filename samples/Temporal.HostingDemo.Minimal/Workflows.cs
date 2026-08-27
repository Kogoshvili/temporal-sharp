using Kogoshvili.Temporal.Hosting;
using Temporalio.Activities;
using Temporalio.Workflows;

namespace Kogoshvili.Temporal.HostingDemo.Minimal;

[Workflow]
public sealed class GreetingWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string name) =>
        await ActivityOps.ExecuteAsync(() => GreetingActivities.Greet(name));
}

public static class GreetingActivities
{
    [Activity]
    public static string Greet(string name) => $"Hello from Kogoshvili.Temporal.Hosting, {name}!";
}
