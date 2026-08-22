using Temporalio.Workflows;

namespace TemporalSharp.SampleApp;

// This workflow uses the deterministic SDK APIs, so the analyzer stays silent.
[Workflow]
public class GoodWorkflow
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        var now = Workflow.UtcNow;
        var id = Workflow.NewGuid();
        await Workflow.DelayAsync(100);
    }
}
