using Kogoshvili.Temporal.Hosting;
using Temporalio.Activities;
using Temporalio.Workflows;

namespace Kogoshvili.Temporal.HostingDemo.Configured;

[Workflow]
public sealed class GreetingWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string name)
    {
        // Resolve the default activity-options preset configured under
        // Temporal:ActivityOptions:Default (ScheduleToCloseTimeout + heartbeat).
        return await Workflow.ExecuteActivityAsync(
            () => StaticActivities.Greet(name),
            ActivityOptionsRegistry.GetDefault()!);
    }
}

/// <summary>
/// Invokes each of the four activity lifetimes and reports the instance id of
/// every call, making the lifetime conventions observable: scoped/singleton/
/// transient instance ids vs the static path.
/// </summary>
[Workflow]
public sealed class LifetimeProbeWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync()
    {
        var options = new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(10) };

        var scoped1 = await Workflow.ExecuteActivityAsync((ScopedActivities activities) => activities.ScopedProbe(), options);
        var scoped2 = await Workflow.ExecuteActivityAsync((ScopedActivities activities) => activities.ScopedProbe(), options);
        var singleton1 = await Workflow.ExecuteActivityAsync((SingletonActivities activities) => activities.SingletonProbe(), options);
        var singleton2 = await Workflow.ExecuteActivityAsync((SingletonActivities activities) => activities.SingletonProbe(), options);
        var transient1 = await Workflow.ExecuteActivityAsync((TransientActivities activities) => activities.TransientProbe(), options);
        var transient2 = await Workflow.ExecuteActivityAsync((TransientActivities activities) => activities.TransientProbe(), options);
        var staticResult = await Workflow.ExecuteActivityAsync(() => StaticActivities.StaticProbe(), options);

        return string.Join(
            '\n',
            $"scoped    : {scoped1} / {scoped2}   (new instance per attempt)",
            $"singleton : {singleton1} / {singleton2}   (same instance every call)",
            $"transient : {transient1} / {transient2}   (new instance per resolution)",
            $"static    : {staticResult}   (no instance)");
    }
}

/// <summary>
/// Receives a payload large enough to trigger the claim-check codec
/// (<c>Temporal:DataConverter:ClaimCheck:ThresholdBytes</c>): it is encrypted,
/// then offloaded to the store, leaving only a reference in the workflow
/// history. The codec server decodes it on demand for the Web UI / CLI.
/// </summary>
[Workflow]
public sealed class ClaimCheckWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string largePayload)
    {
        // Named preset from Temporal:ActivityOptions:Presets:long-running.
        var length = await Workflow.ExecuteActivityAsync(
            () => StaticActivities.Measure(largePayload),
            ActivityOptionsRegistry.Get("long-running"));

        return $"Claim-check demo: activity received {length} characters " +
            $"(first 40: \"{largePayload[..40]}\").";
    }
}
