# Workflow ops, child workflows, and settings

`IWorkflowOps` is a typed facade over the Temporal client for starting,
signaling, querying, and otherwise managing workflows, resolving task queue,
workflow ID, and options from `Temporal:Workflows` config. This page also covers
child workflows (`ChildWorkflowOps`), workflow-ID conventions, and per-workflow
settings (`WorkflowSettings`).

## Minimal setup

With a task queue configured, starting a zero-argument workflow is a single
call — the type comes from the generic and the queue, ID, and timeouts resolve
automatically:

```csharp
[Workflow]
public sealed class GreetingWorkflow
{
    [WorkflowRun]
    public async Task RunAsync() { /* ... */ }
}

public sealed class MyService
{
    private readonly IWorkflowOps workflows;
    public MyService(IWorkflowOps workflows) => this.workflows = workflows;

    public async Task RunAsync()
    {
        await workflows.StartAsync<GreetingWorkflow>();
    }
}
```

The task queue is required somewhere — `ByType` override, `Default` preset, or an
explicit argument — otherwise the start throws an `InvalidOperationException`.
The minimal configuration that makes the above work:

```json
{
  "Temporal": {
    "Workflows": {
      "Default": {
        "TaskQueue": "my-queue"
      }
    }
  }
}
```

## Configuration

### Task queue and workflow ID

`Temporal:Workflows:Default:TaskQueue` is the fallback start queue. Workflow IDs
are generated from a template under `Temporal:Workflows:Id`:

```json
{
  "Temporal": {
    "Workflows": {
      "Id": {
        "Format": "{Type:s}-{Guid:N}",
        "ChildFormat": "{Type:s}-{Guid:N}-{Parent}"
      },
      "Default": {
        "TaskQueue": "my-queue"
      }
    }
  }
}
```

The `Format` template supports these placeholders:

- `{Type}` — full workflow type name (e.g. `GreetingWorkflow`).
- `{Type:s}` — the type name with a trailing `workflow` (case-insensitive) stripped.
- `{Queue}` — the resolved task queue.
- `{Guid}` / `{Guid:N}` / `{Guid:D}` / `{Guid:B}` — a new GUID, with an optional format.
- `{Parent}` — the parent workflow's ID (child format only).

When unset, `{Type:s}-{Guid:N}` applies for client starts and
`{Type:s}-{Guid:N}-{Parent}` for child starts. Set a template to `""` to opt out
and let the SDK generate the ID.

### Per-type overrides

`ByType` entries are keyed by workflow type name and override the `Default`
preset for that type:

```json
{
  "Temporal": {
    "Workflows": {
      "Default": {
        "TaskQueue": "my-queue",
        "RunTimeout": "00:05:00"
      },
      "ByType": {
        "GreetingWorkflow": {
          "RunTimeout": "00:01:00"
        }
      }
    }
  }
}
```

Every preset property is nullable; a `null` value leaves the SDK default
untouched. Timeouts are time-span strings.

## Full configuration

### Precedence

Options resolve lowest to highest:

1. SDK defaults,
2. the `Default` preset,
3. the `ByType` override,
4. explicit `taskQueue` / `workflowId` / `configure` arguments.

The `configure` callback is applied last and always wins:

```csharp
await workflows.StartAsync<GreetingWorkflow>(
    taskQueue: "vip-queue",
    workflowId: "vip-0001",
    configure: o => o.RunTimeout = TimeSpan.FromMinutes(30));
```

### Preset fields

The `WorkflowOptionsPreset` exposes these fields (all optional):

- `RunTimeout`, `TaskTimeout`, `ExecutionTimeout` — time-span strings.
- `IdConflictPolicy` — `Fail`, `UseExisting`, or `TerminateExisting`.
- `StartDelay` — time to wait before the workflow starts.
- `Retry` — a retry policy object (see below).
- `TaskQueue` — the start queue (fallback on `Default`, override on `ByType`).
- `ParentClosePolicy`, `CancellationType` — child workflows only; ignored for
  client starts.

A retry policy configures backoff and error filtering:

```json
{
  "Temporal": {
    "Workflows": {
      "Default": {
        "TaskQueue": "my-queue",
        "Retry": {
          "InitialInterval": "00:00:01",
          "BackoffCoefficient": 2.0,
          "MaximumInterval": "00:01:00",
          "MaximumAttempts": 3,
          "NonRetryableErrorTypes": [ "MyValidationException" ]
        }
      }
    }
  }
}
```

### Start overloads

`IWorkflowOps.StartAsync` comes in several shapes, all sharing the trailing
`taskQueue` / `workflowId` / `configure` parameters:

- Lambda, invoking the run method:
  `StartAsync<TWorkflow>(w => w.RunAsync(...))` (or `<TWorkflow, TResult>` for a
  typed-result handle).
- Single-parameter object, passed directly:
  `StartAsync<TWorkflow, TParams>(input)` (or `<TWorkflow, TParams, TResult>`).
- Zero-argument: `StartAsync<TWorkflow>()` (or `<TWorkflow, TResult>`).
- String/name-based: `StartAsync(workflow, args, ...)`, for dynamic workflow
  types with no static class.

```csharp
// Lambda with typed result.
var h1 = await workflows.StartAsync<MoneyTransferWorkflow, string>(
    w => w.RunAsync("acct-1", "acct-2", 100m));

// Single-parameter object, typed result.
var h2 = await workflows.StartAsync<MoneyTransferWorkflow, MoneyTransferInput, string>(
    new MoneyTransferInput("acct-1", "acct-2", 100m));

// Zero-argument, typed result.
var h3 = await workflows.StartAsync<GreetingWorkflow, string>();

// By name (dynamic type).
var h4 = await workflows.StartAsync("MoneyTransferWorkflow",
    new object?[] { "acct-1", "acct-2", 100m });
```

### Signal, query, result, terminate, cancel, list, restart

```csharp
await workflows.SignalAsync<MoneyTransferWorkflow>("id-1", w => w.ApproveAsync("a42"));
var status = await workflows.QueryAsync<MoneyTransferWorkflow, string>("id-1", w => w.Status());
var result = await workflows.ResultAsync<string>("id-1");
await workflows.TerminateAsync("id-1", reason: "rollback");
await workflows.CancelAsync("id-1");

await foreach (var exec in workflows.ListAsync("WorkflowType = 'MoneyTransferWorkflow'"))
    Console.WriteLine(exec.Id);
```

`RestartAsync` terminates the current run (best-effort, swallowing
`NotFound`) and starts a fresh run with a new ID, returning the new handle:

```csharp
var handle = await workflows.RestartAsync<MoneyTransferWorkflow>(
    "id-1", w => w.RunAsync("acct-1", "acct-2", 100m));
```

The `RunAsync` call (`w => w.RunAsync(...)`) determines the workflow type and
arguments for the new run; `taskQueue` and `configure` are the only remaining
knobs (the ID is always regenerated).

### Child workflows

Any workflow can run as a child. `ChildWorkflowOps` is a static, workflow-side
facade that resolves `ChildWorkflowOptions` from the same `Temporal:Workflows`
`Default`/`ByType` config and applies the `ChildFormat` ID convention, so a
workflow behaves consistently whether started from a client or as a child:

```csharp
[Workflow]
public sealed class ParentWorkflow
{
    [WorkflowRun]
    public async Task RunAsync(string orderId)
    {
        // Await the child's result.
        var result = await ChildWorkflowOps.ExecuteAsync<BillingWorkflow, string, string>(orderId);

        // Fire-and-forget: start and get the handle for signaling/querying.
        var handle = await ChildWorkflowOps.StartAsync<BillingWorkflow, string>(orderId);
    }
}
```

Like the client facade, `ExecuteAsync`/`StartAsync` come in lambda, single-
parameter, zero-argument, and name-based overloads. Precedence (lowest to
highest): SDK defaults, `Default`, `ByType`, then an explicit
`ChildWorkflowOptions` argument:

```csharp
var other = await ChildWorkflowOps.ExecuteAsync(
    (BillingWorkflow wf) => wf.RunAsync(orderId),
    new ChildWorkflowOptions { ParentClosePolicy = ParentClosePolicy.Abandon });
```

Child-only preset fields (`ParentClosePolicy`, `CancellationType`) apply here;
client-only fields (`StartDelay`, `IdConflictPolicy`) are ignored.

### Workflow settings

`Temporal:WorkflowSettings` lets a workflow read its own typed configuration for
values the caller should not have to supply at start time (a batch size, an
endpoint, a feature flag). It is keyed per type and merged over an optional
default:

```json
{
  "Temporal": {
    "WorkflowSettings": {
      "Default": { "batchSize": 10 },
      "ByType": {
        "BatchingWorkflow": { "batchSize": 100 }
      }
    }
  }
}
```

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

`GetAsync<TSettings>()` reads through a built-in local activity, so the value is
recorded in workflow history and stays deterministic across replay even when the
configuration is live-reloaded mid-run. Values are converted to natural JSON
types (`bool`/number/string), and `TSettings` can be any
`System.Text.Json`-deserializable type. Read once at the top of the workflow and
reuse the value to keep a single run internally consistent.
