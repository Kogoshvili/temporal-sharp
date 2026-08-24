using Temporalio.Workflows;

namespace Kogoshvili.Temporal.Testing.Tests;

[Workflow]
public class GreetingWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string name)
    {
        await Workflow.DelayAsync(TimeSpan.FromMilliseconds(1));
        return $"Hello, {name}!";
    }
}
