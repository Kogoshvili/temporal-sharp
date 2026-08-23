using Temporalio.Workflows;

namespace TemporalSharp.SampleApp;

// SDK-contract violations for the workflow entry method (TMP3201).
public static class ContractViolations
{
    // TMP3201 — [WorkflowRun] method is not public.
    [Workflow]
    public class NonPublicRunWorkflow
    {
        [WorkflowRun]
        private Task Run() => Task.CompletedTask;
    }

    // TMP3201 — [WorkflowRun] method does not return Task.
    [Workflow]
    public class NonTaskRunWorkflow
    {
        [WorkflowRun]
        public void Run() { }
    }

    // TMP3201 — [WorkflowRun] method is not in a [Workflow] type.
    public class RunWithoutWorkflow
    {
        [WorkflowRun]
        public Task Run() => Task.CompletedTask;
    }

    // TMP3201 — a [Workflow] type with more than one [WorkflowRun] method.
    [Workflow]
    public class MultipleRunWorkflow
    {
        [WorkflowRun]
        public Task Run1() => Task.CompletedTask;

        [WorkflowRun]
        public Task Run2() => Task.CompletedTask;
    }
}
