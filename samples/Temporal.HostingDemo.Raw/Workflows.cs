using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Workflows;

namespace Kogoshvili.Temporal.HostingDemo.Raw;

/// <summary>
/// The hand-rolled equivalent of the starter's
/// <c>ActivityOptionsRegistry</c>, which is seeded from
/// <c>Temporal:ActivityOptions</c> in <c>appsettings.json</c>. Workflows cannot
/// use dependency injection, so presets live in static state that is fixed
/// before any workflow runs (deterministic during replay).
/// </summary>
public static class ActivityOptionsPresets
{
    public static ActivityOptions Default { get; } =
        new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5), HeartbeatTimeout = TimeSpan.FromSeconds(30) };

    public static ActivityOptions Get(string name) => name switch
    {
        "long-running" => new ActivityOptions
        {
            ScheduleToCloseTimeout = TimeSpan.FromMinutes(30),
            HeartbeatTimeout = TimeSpan.FromMinutes(1),
        },
        _ => throw new KeyNotFoundException($"No preset named '{name}'."),
    };
}

[Workflow]
public sealed class GreetingWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string name)
    {
        return await Workflow.ExecuteActivityAsync(
            () => StaticActivities.Greet(name),
            ActivityOptionsPresets.Default);
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

/// <summary>
/// Receives a payload large enough to trigger the claim-check codec built by
/// hand in <c>Program.cs</c>: encrypted, then offloaded to the store.
/// </summary>
[Workflow]
public sealed class ClaimCheckWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string largePayload)
    {
        var length = await Workflow.ExecuteActivityAsync(
            () => StaticActivities.Measure(largePayload),
            ActivityOptionsPresets.Get("long-running"));

        return $"Claim-check demo: activity received {length} characters " +
            $"(first 40: \"{largePayload[..40]}\").";
    }
}

/// <summary>
/// The hand-rolled equivalent of the starter's <c>Saga</c> helper (which is a
/// port of the Java SDK's Saga): a plain <c>List&lt;Func&lt;Task&gt;&gt;</c> of
/// compensations, registered before each forward activity and unwound in
/// reverse (LIFO) order on failure.
/// </summary>
[Workflow]
public sealed class SagaWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string orderId)
    {
        var compensations = new List<Func<Task>>();
        var options = new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(10) };
        var compensationsRun = new List<string>();

        try
        {
            compensations.Add(async () =>
                compensationsRun.Add(await Workflow.ExecuteActivityAsync(
                    () => StaticActivities.CancelReservation(orderId), options)));

            await Workflow.ExecuteActivityAsync(() => StaticActivities.Reserve(orderId), options);

            compensations.Add(async () =>
                compensationsRun.Add(await Workflow.ExecuteActivityAsync(
                    () => StaticActivities.CancelAllocation(orderId), options)));

            await Workflow.ExecuteActivityAsync(() => StaticActivities.Allocate(orderId), options);

            await Workflow.ExecuteActivityAsync(() => StaticActivities.Charge(orderId), options);
        }
        catch (Exception ex)
        {
            Workflow.Logger.LogWarning(ex, "Charge failed; compensating");
            compensations.Reverse();
            foreach (var compensation in compensations)
            {
                await compensation();
            }

            return $"compensated in LIFO order: {string.Join(", ", compensationsRun)}";
        }

        return "completed without compensation";
    }
}

/// <summary>
/// The hand-rolled download (manual heartbeat + resume) via the "long-running"
/// preset, which carries a HeartbeatTimeout.
/// </summary>
[Workflow]
public sealed class DownloadWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(int totalBytes)
    {
        var downloaded = await Workflow.ExecuteActivityAsync(
            () => ManualHeartbeatActivities.DownloadAsync(totalBytes),
            ActivityOptionsPresets.Get("long-running"));

        return $"Downloaded {downloaded}/{totalBytes} bytes.";
    }
}
