using Temporalio.Workflows;

namespace Kogoshvili.Temporal.SampleApp;

// Workflow versioning (patching) misuse (TMP33xx).
[Workflow]
public class VersioningViolations
{
    [WorkflowRun]
    public async Task RunAsync(string patchId)
    {
        // TMP3301 — the same patch id is both Patched and DeprecatePatch'd.
        if (Workflow.Patched("v1"))
        {
            Workflow.DeprecatePatch("v1");
        }

        // TMP3302 — patch id is not a constant string.
        if (Workflow.Patched(patchId))
        {
            await Task.CompletedTask;
        }
    }
}

// TMP4108 — busy-polling the worker-deployment-version flag on a timer; the
// flag only refreshes at workflow task boundaries.
[Workflow]
public class VersionFlagPollingViolations
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        while (!Workflow.TargetWorkerDeploymentVersionChanged)
        {
            await Workflow.DelayAsync(1000);
        }
    }
}
