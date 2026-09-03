using Temporalio.Workflows;

namespace Kogoshvili.Temporal.MapSmoke.AppA.Contracts;

// Scenario 11: [Workflow] interface contract — the run method is declared
// here, not implemented; AppA.Worker's OrderWorkflow is its single
// implementation, so typed calls against the interface resolve to it.
[Workflow]
public interface IOrderWorkflow
{
    [WorkflowRun]
    Task<string> RunAsync(string order);
}
