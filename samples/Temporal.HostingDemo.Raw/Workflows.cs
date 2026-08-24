using Temporalio.Activities;
using Temporalio.Workflows;

namespace Kogoshvili.Temporal.HostingDemo.Raw;

[Workflow]
public sealed class GreetingWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string name)
    {
        return await Workflow.ExecuteActivityAsync(
            () => StaticActivities.Greet(name),
            new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(10) });
    }
}

/// <summary>
/// Invokes each of the four activity lifetimes and reports the instance id of
/// every call, so the manual registration done in <c>Program.cs</c> is
/// observable: scoped/singleton/transient instance ids vs the static path.
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
