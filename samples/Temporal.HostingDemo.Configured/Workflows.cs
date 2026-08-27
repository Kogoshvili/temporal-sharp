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

/// <summary>
/// Settings type for <see cref="BatchingWorkflow"/>, bound from
/// <c>Temporal:WorkflowSettings:ByType:BatchingWorkflow</c> (merged over
/// <c>Default</c>).
/// </summary>
public sealed class BatchingSettings
{
    public int BatchSize { get; set; }
}

/// <summary>
/// Reads its own workflow-level settings via <see cref="WorkflowSettings"/> — a
/// local activity that returns the config snapshot, so it stays deterministic
/// across replay even when <c>Temporal:WorkflowSettings</c> is live-reloaded.
/// This is for settings a caller shouldn't have to know when starting the
/// workflow.
/// </summary>
[Workflow]
public sealed class BatchingWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync()
    {
        var settings = await WorkflowSettings.GetAsync<BatchingSettings>();

        return $"Workflow settings: batch size = {settings.BatchSize}";
    }
}

/// <summary>
/// Demonstrates the <see cref="Saga"/> compensation helper (a port of the Java
/// SDK's Saga). Each forward activity registers a compensation <em>before</em>
/// it runs; when <c>Charge</c> fails, <c>CompensateAsync</c> unwinds them in
/// reverse (LIFO) order. Compensations are ordinary activity calls, so their
/// retry policy and timeouts come from <see cref="ActivityOptions"/> as usual.
/// </summary>
[Workflow]
public sealed class SagaWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string orderId)
    {
        var saga = new Saga();
        var options = new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(10) };
        var compensationsRun = new List<string>();

        try
        {
            saga.AddCompensation(async () =>
                compensationsRun.Add(await Workflow.ExecuteActivityAsync(
                    () => StaticActivities.CancelReservation(orderId), options)));

            await Workflow.ExecuteActivityAsync(() => StaticActivities.Reserve(orderId), options);

            saga.AddCompensation(async () =>
                compensationsRun.Add(await Workflow.ExecuteActivityAsync(
                    () => StaticActivities.CancelAllocation(orderId), options)));

            await Workflow.ExecuteActivityAsync(() => StaticActivities.Allocate(orderId), options);

            // Always fails, so the two compensations run in LIFO order:
            // cancel-allocation, then cancel-reservation.
            await Workflow.ExecuteActivityAsync(() => StaticActivities.Charge(orderId), options);
        }
        catch (Exception ex)
        {
            Workflow.Logger.LogWarning(ex, "Charge failed; compensating");
            await saga.CompensateAsync();
            return $"compensated in LIFO order: {string.Join(", ", compensationsRun)}";
        }

        return "completed without compensation";
    }
}
