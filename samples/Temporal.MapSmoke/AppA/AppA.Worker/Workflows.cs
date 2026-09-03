using Kogoshvili.Temporal.MapSmoke.AppA.Contracts;
using Kogoshvili.Temporal.MapSmoke.AppB.Worker;
using Temporalio.Common;
using Temporalio.Workflows;

namespace Kogoshvili.Temporal.MapSmoke.AppA.Worker;

// Scenario 1: MainWorkflow exercises every map feature in one body — typed
// calls, a repeated call, a looped call, a string-named call, a local
// activity, a cross-queue routed call, a child workflow, and a Nexus call.
[Workflow]
public sealed class MainWorkflow
{
    private readonly List<string> greetings = [];

    [WorkflowSignal]
    public Task AddGreetingAsync(string greeting)
    {
        greetings.Add(greeting);
        return Task.CompletedTask;
    }

    [WorkflowQuery]
    public string GetGreeting() => greetings.LastOrDefault() ?? string.Empty;

    [WorkflowRun]
    public async Task RunAsync(string name)
    {
        // Scenario 1a: typed activity call (encounter ordinal #1).
        var greeting = await Workflow.ExecuteActivityAsync(
            (MainActivities acts) => acts.Greet(name),
            new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(10) });

        // Scenario 1c: activity call inside a foreach loop (loop-marked ordinals).
        var counts = new List<string>();
        foreach (var i in new[] { 1, 2, 3 })
        {
            counts.Add(await Workflow.ExecuteActivityAsync(
                (MainActivities acts) => acts.Counter(i),
                new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(10) }));
        }

        // Scenario 1d: string-named activity call — resolved cross-solution to
        // BActivities.RecordLegacyPayment via its [Activity("LegacyPayment")] name.
        await Workflow.ExecuteActivityAsync(
            "LegacyPayment",
            new object?[] { name },
            new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(10) });

        // Scenario 12: call through an interface that has no implementation
        // anywhere — stays on the contract node, marked ❔.
        await Workflow.ExecuteActivityAsync(
            (IGhostActivities g) => g.Vanish(name),
            new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(10) });

        // Scenario 1b: second call to the same Greet activity (later ordinal).
        var greeting2 = await Workflow.ExecuteActivityAsync(
            (MainActivities acts) => acts.Greet(name),
            new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(10) });

        // Scenario 1e: local activity call.
        var local = await Workflow.ExecuteLocalActivityAsync(
            (MainActivities acts) => acts.Local(name),
            new LocalActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(10) });

        // Scenario 1f: cross-queue routed call to AppB's activity on queue-b.
        var processed = await Workflow.ExecuteActivityAsync(
            (BActivities acts) => acts.Process(name),
            new ActivityOptions { TaskQueue = "queue-b", StartToCloseTimeout = TimeSpan.FromSeconds(10) });

        // Scenario 1g: typed child workflow start.
        var child = await Workflow.StartChildWorkflowAsync((ChildWorkflow w) => w.RunAsync(name));
        var childResult = await child.GetResultAsync();

        // Scenario 1h: string-named Nexus operation start.
        var shipping = Workflow.CreateNexusWorkflowClient("shipping", "shipping-nexus");
        await shipping.StartNexusOperationAsync("Ship", new ShippingRequest(name));

        _ = string.Join(", ", greeting, greeting2, local, processed, childResult, counts.Count);
    }
}

// Scenario 3: ChildWorkflow — started by MainWorkflow, has its own query.
[Workflow]
public sealed class ChildWorkflow
{
    [WorkflowQuery]
    public string GetStatus() => "ready";

    [WorkflowRun]
    public async Task<string> RunAsync(string name)
    {
        return await Workflow.ExecuteActivityAsync(
            (MainActivities acts) => acts.Greet(name),
            new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(10) });
    }
}

// Scenario 4: DualQueueWorkflow — Program.cs registers it on two task queues
// (queue-a and queue-c), so it must render with multiple queue edges.
[Workflow]
public sealed class DualQueueWorkflow
{
    [WorkflowRun]
    public Task RunAsync(string name) => Task.CompletedTask;
}

// Scenario 5: ConfigQueueWorkflow — registered via a non-constant (env-derived)
// queue name, so its queue must render as Unknown.
[Workflow]
public sealed class ConfigQueueWorkflow
{
    [WorkflowRun]
    public Task RunAsync(string name) => Task.CompletedTask;
}

// Scenario 11: OrderWorkflow implements the IOrderWorkflow contract from
// AppA.Contracts — activities, the child workflow, and the external signal
// are all expressed through typed interfaces.
[Workflow]
public sealed class OrderWorkflow : IOrderWorkflow
{
    private readonly List<string> approvals = [];

    [WorkflowRun]
    public async Task<string> RunAsync(string order)
    {
        // Scenario 11a: interface-typed activity call with a retry policy.
        var processed = await Workflow.ExecuteActivityAsync(
            (IOrderActivities acts) => acts.Process(order),
            new ActivityOptions
            {
                StartToCloseTimeout = TimeSpan.FromSeconds(30.0),
                RetryPolicy = new() { MaximumAttempts = 3 },
            });

        // Scenario 11b: interface-typed Ship call outside the loop...
        await Workflow.ExecuteActivityAsync(
            (IOrderActivities acts) => acts.Ship(order),
            new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(10.0) });

        // ...and again inside a foreach loop (loop-marked ordinal).
        foreach (var warehouse in new[] { "east", "west" })
        {
            await Workflow.ExecuteActivityAsync(
                (IOrderActivities acts) => acts.Ship(order),
                new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(10.0) });
        }

        // Scenario 11c: ambiguous contract call — two implementations exist
        // (only AmbiguousImplA is registered), so the grapher renders a
        // Contract node instead of picking one.
        await Workflow.ExecuteActivityAsync(
            (IAmbiguousActivities acts) => acts.Run(order),
            new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(10.0) });

        // Scenario 11d: child workflow started through the [Workflow] interface.
        var child = await Workflow.StartChildWorkflowAsync<IOrderWorkflow, string>(
            (IOrderWorkflow w) => w.RunAsync(order));
        var childResult = await child.GetResultAsync();

        // Scenario 11e: signal an external workflow via a typed handle.
        var external = Workflow.GetExternalWorkflowHandle<OtherWorkflow>("other");
        await external.SignalAsync((OtherWorkflow w) => w.Poke());

        return $"{processed}/{childResult}";
    }

    // Scenario 14: signal/query/update handlers exercised from Program.cs.
    [WorkflowSignal]
    public Task ApproveAsync(string approver)
    {
        approvals.Add(approver);
        return Task.CompletedTask;
    }

    [WorkflowQuery]
    public string GetStatus() => approvals.Count > 0 ? "approved" : "pending";

    [WorkflowUpdate]
    public async Task<string> SetPriorityAsync(int priority)
    {
        await Workflow.DelayAsync(TimeSpan.FromMilliseconds(priority));
        return $"priority-{priority}";
    }
}

// Scenario 11e/15: OtherWorkflow — receives a cross-workflow signal from
// OrderWorkflow and is registered on the env-default queue (Scenario 15).
[Workflow]
public sealed class OtherWorkflow
{
    private readonly List<string> pokes = [];

    [WorkflowSignal]
    public Task Poke()
    {
        pokes.Add("poke");
        return Task.CompletedTask;
    }

    [WorkflowRun]
    public Task RunAsync(string name) => Task.CompletedTask;
}

// Scenario 12: HeartbeatWorkflow — one heartbeating activity and one silent
// activity, both called with HeartbeatTimeout so the missing heartbeat
// renders as a heartbeat-issue edge.
[Workflow]
public sealed class HeartbeatWorkflow
{
    [WorkflowRun]
    public async Task RunAsync(string name)
    {
        await Workflow.ExecuteActivityAsync(
            (HeartbeatActivities acts) => acts.HeartbeatGood(name),
            new ActivityOptions
            {
                StartToCloseTimeout = TimeSpan.FromSeconds(10.0),
                HeartbeatTimeout = TimeSpan.FromSeconds(10.0),
            });

        await Workflow.ExecuteActivityAsync(
            (HeartbeatActivities acts) => acts.HeartbeatMissing(name),
            new ActivityOptions
            {
                StartToCloseTimeout = TimeSpan.FromSeconds(10.0),
                HeartbeatTimeout = TimeSpan.FromSeconds(5.0),
            });
    }
}

// Scenario 16: ConfigFileWorkflow — registered on the queue read from
// appsettings.json ("config-q"; "config-q-prod" in Production).
[Workflow]
public sealed class ConfigFileWorkflow
{
    [WorkflowRun]
    public Task RunAsync(string name) => Task.CompletedTask;
}

public sealed record ShippingRequest(string Name);
