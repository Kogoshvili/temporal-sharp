// A workflow with no WorkflowReplayer-based replay test anywhere in the project.
// TMP5001 flags this: the workflow can be replayed by re-execution, and without a
// replay test a non-deterministic change silently breaks existing histories.
namespace Kogoshvili.Temporal.Demo;

[Temporalio.Workflows.Workflow]
public class GreetingWorkflow
{
    [Temporalio.Workflows.WorkflowRun]
    public System.Threading.Tasks.Task<string> RunAsync(string name)
        => System.Threading.Tasks.Task.FromResult($"Hello, {name}!");
}
