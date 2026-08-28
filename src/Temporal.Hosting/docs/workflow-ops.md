# Workflow-options presets, ID conventions, child workflows, and settings

## Workflow-options presets and ID conventions

`Temporal:Workflows` configures how workflows are *started* (client-side), as
opposed to `ActivityOptions` which configures how activities *execute*. It
defines a default preset, per-workflow-type overrides, and a workflow-ID
template. The `IWorkflowOps` facade wraps the client and these options behind a
single typed API — workflow type comes from the generic argument, and the task
queue / workflow ID / timeouts resolve from config (each overridable per call):

```csharp
public class MyService
{
    private readonly IWorkflowOps workflows;

    public MyService(IWorkflowOps workflows) => this.workflows = workflows;

    public async Task RunAsync()
    {
        // Type from the generic; queue, ID, and timeouts from Temporal:Workflows.
        var handle = await workflows.StartAsync<MoneyTransferWorkflow, string>(
            w => w.RunAsync("acct-1", "acct-2", 100m));

        var result = await handle.GetResultAsync();

        // Override anything per-call (explicit args always win):
        await workflows.StartAsync<MoneyTransferWorkflow>(
            w => w.RunAsync("acct-3", "acct-4", 50m),
            taskQueue: "vip-payments",
            workflowId: "vip-transfer-0001");

        // Signal / query / result / terminate / cancel / list / restart:
        await workflows.SignalAsync<MoneyTransferWorkflow>("vip-transfer-0001", w => w.ApproveAsync("a42"));
        var status = await workflows.QueryAsync<MoneyTransferWorkflow, string>("vip-transfer-0001", w => w.Status());
        await workflows.TerminateAsync("vip-transfer-0001", reason: "rollback");
        await foreach (var exec in workflows.ListAsync("WorkflowType = 'MoneyTransferWorkflow'"))
            Console.WriteLine(exec.Id);

        // String-based (no workflow type) overloads mirror the typed ones:
        await workflows.StartAsync("MoneyTransferWorkflow", new object?[] { "acct-1", "acct-2", 100m });
    }
}
```

For workflows that take a single input object (the common "parameter object"
convention), the run argument can be passed directly without a lambda:

```csharp
public sealed record MoneyTransferInput(string From, string To, decimal Amount);

await workflows.StartAsync<MoneyTransferWorkflow, MoneyTransferInput>(
    new MoneyTransferInput("acct-1", "acct-2", 100m));
```

Here `TParams` is the workflow's single run parameter (i.e.
`RunAsync(MoneyTransferInput)`). Use the lambda overloads for workflows with
multiple run parameters.

The two-generic form above returns a `WorkflowHandle<TWorkflow>` (untyped
result). To also get a typed result, add a third generic:

```csharp
var handle = await workflows.StartAsync<MoneyTransferWorkflow, MoneyTransferInput, string>(
    new MoneyTransferInput("acct-1", "acct-2", 100m));

string receipt = await handle.GetResultAsync(); // typed, no extra generic needed
```

Workflows with no run parameters can omit the argument entirely:

```csharp
var handle = await workflows.StartAsync<GreetingWorkflow, string>();
string result = await handle.GetResultAsync(); // typed

// Void-result workflows drop the second generic:
await workflows.StartAsync<OneWayWorkflow>();
```

Precedence (lowest to highest): SDK defaults → `Default` preset → `ByType`
override → the caller's explicit `taskQueue`/`workflowId`/`configure`. The
preset exposes `RunTimeout`, `TaskTimeout`, `ExecutionTimeout`,
`IdConflictPolicy`, `StartDelay`, `Retry`, `TaskQueue` (the start queue), and —
for child workflows — `ParentClosePolicy` and `CancellationType`.
The task queue is resolved from `ByType` then `Default`; if none is set and none
is passed explicitly, the start throws an `InvalidOperationException` with a
clear message rather than failing obscurely.

The `Id:Format` template supports `{Type}` (full name) and `{Type:s}`
(trailing "workflow" stripped, case-insensitive), `{Queue}`, and `{Guid}` (plus
`{Guid:N}`/`{Guid:D}`/`{Guid:B}`); `Id:ChildFormat` additionally supports
`{Parent}` (the parent workflow's ID). When no format is set, a shipped default
applies — `{Type:s}-{Guid:N}` for client starts, `{Type:s}-{Guid:N}-{Parent}` for
child starts. Set a template to the empty string (`""`) to opt out and defer to
the SDK's generated ID.

## Child-workflow ops

Any workflow can be started as a child. `ChildWorkflowOps` (static, workflow-side)
resolves a child's `ChildWorkflowOptions` from the same `Temporal:Workflows`
`Default`/`ByType` config and applies the child ID convention, so a workflow
behaves consistently whether it is started from a client or as a child. Precedence
(lowest to highest): SDK defaults → `Default` → `ByType` → the explicit
`ChildWorkflowOptions` you pass to the call.

```csharp
[Workflow]
public sealed class ParentWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string orderId)
    {
        // Options and child ID resolve from config; no per-call plumbing.
        var result = await ChildWorkflowOps.ExecuteAsync(
            (BillingWorkflow wf) => wf.RunAsync(orderId));

        // Fire-and-forget: start and get the handle for signaling/querying.
        var handle = await ChildWorkflowOps.StartAsync(
            (BillingWorkflow wf) => wf.RunAsync(orderId));

        // Override just this call with explicit child options:
        var other = await ChildWorkflowOps.ExecuteAsync(
            (BillingWorkflow wf) => wf.RunAsync(orderId),
            new ChildWorkflowOptions { ParentClosePolicy = ParentClosePolicy.Abandon });

        return result;
    }
}
```

For child workflows whose run method takes a single parameter, the argument can
also be passed directly without a lambda:

```csharp
// Terse execute, typed result (third generic is the result type):
var result = await ChildWorkflowOps.ExecuteAsync<BillingWorkflow, string, string>(orderId);

// Terse fire-and-forget start, returns the child handle:
var handle = await ChildWorkflowOps.StartAsync<BillingWorkflow, string>(orderId);
```

Child workflows with no run parameters drop the argument the same way:

```csharp
var result = await ChildWorkflowOps.ExecuteAsync<BillingWorkflow, string>();
var handle = await ChildWorkflowOps.StartAsync<BillingWorkflow>();
```

`ExecuteAsync` awaits the child's result; `StartAsync` returns a
`ChildWorkflowHandle` so you can signal the running child before awaiting
`GetResultAsync()`. A typed-result `StartAsync` (returning
`ChildWorkflowHandle<TWorkflow, TResult>`) is not offered because the SDK's child
handles are not user-constructible — use `ExecuteAsync<..., TResult>` for the
typed-result run-and-await path.

## Workflow settings

`Temporal:WorkflowSettings` lets a workflow read its own typed configuration,
for settings the caller can't or shouldn't supply when starting the workflow
(e.g. a batch size, an endpoint, a feature flag). It is keyed per workflow type
and merged over an optional default:

```json
{
  "Temporal": {
    "WorkflowSettings": {
      "Default": { "batchSize": 10 },
      "ByType": { "BatchingWorkflow": { "batchSize": 100 } }
    }
  }
}
```

Inside the workflow, resolve the settings through the static
`WorkflowSettings` facade:

```csharp
public sealed class BatchingSettings
{
    public int BatchSize { get; set; }
}

[Workflow]
public sealed class BatchingWorkflow
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        var settings = await WorkflowSettings.GetAsync<BatchingSettings>();
        // use settings.BatchSize ...
    }
}
```

`GetAsync` reads through a built-in local activity, so the value is recorded in
workflow history and stays stable across replay even if the configuration is
live-reloaded mid-run (only new runs pick up the change). Read once at the top
of the workflow and reuse the value to keep a single run internally consistent.

Settings values are typed as JSON (`bool`/number/string) automatically; define
`TSettings` as any `System.Text.Json`-deserializable type (a class with settable
properties is the simplest).
