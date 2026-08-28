# Kogoshvili.Temporal.Hosting

A generic-host worker starter for the [Temporal](https://temporal.io) .NET SDK,
inspired by the Java Spring Boot Temporal starter. It layers configuration
binding, convention-based auto-discovery, a metrics interceptor, and a
test-server toggle on top of
[`Temporalio.Extensions.Hosting`](https://github.com/temporalio/sdk-dotnet/tree/main/src/Temporalio.Extensions.Hosting).

Unlike `Kogoshvili.Temporal.Analyzers`, this library references the **real**
[`Temporalio`](https://www.nuget.org/packages/Temporalio) SDK and targets
**net8.0**.

## What it provides

- **`AddTemporal`** — registers a lazy `ITemporalClient` plus the starter
  services, bound from the `Temporal` configuration section or an options
  delegate.
- **`AddTemporalWorker`** — registers a hosted worker for a task queue. Types
  are registered explicitly on the returned builder (`AddWorkflow<T>()`,
  `AddSingletonActivities<T>()`, ...) or via opt-in auto-discovery.
- **`AddDiscoveredTypes`** — opt-in convention-based auto-discovery of
  `[Workflow]`/`[Activity]` types for a worker.
- **Per-queue tuning** — `Temporal:Workers:<queue>` applies concurrency,
  graceful-shutdown, and cache knobs to a worker.
- **Connection transport options** — `Temporal:KeepAlive`,
  `Temporal:HttpConnectProxy`, `Temporal:DnsLoadBalancing`, and
  `Temporal:GrpcCompression` map onto the SDK's connection options.
- **Activity-options presets** — `Temporal:ActivityOptions` defines default,
  local-default, and named presets (each mapping to both regular and local
  activities), resolved from workflows via the static `ActivityOps` facade
  (workflows cannot use DI).
- **Workflow-options presets & ID conventions** — `Temporal:Workflows` defines a
  default and per-type `WorkflowOptions` presets plus a workflow-ID template,
  surfaced through the typed `IWorkflowOps` facade (start/signal/query/result/
  terminate/cancel/restart/list); caller overrides win.
- **Child-workflow ops** — `ChildWorkflowOps` (static, workflow-side) starts
  child workflows over `Workflow.ExecuteChildWorkflowAsync` /
  `StartChildWorkflowAsync`, resolving `ChildWorkflowOptions` from the same
  `Temporal:Workflows` config (any workflow can run as a child) plus the child
  workflow-ID convention; pass an explicit `ChildWorkflowOptions` to override a
  single call.
- **Workflow settings** — `Temporal:WorkflowSettings` lets a workflow read its own
  typed configuration via `WorkflowSettings.GetAsync<TSettings>()`, useful when
  the caller can't supply the value.
- **Saga/compensation** — a `Saga` helper (a port of the Java SDK's `Saga`) that
  collects compensation operations and unwinds them LIFO on failure, with
  sequential/parallel modes and stop-or-continue error handling.
- **Long-running activity heartbeats** — a `HeartbeatingActivity` base class that
  wraps heartbeat, cancellation-checking, and checkpoint resume
  (`Heartbeat`, `CheckCancellation`, `LoadProgressAsync<T>`), plus an opt-in
  `StartAutoHeartbeat()` background loop that keeps the activity alive and relays
  the last checkpoint rather than clobbering it with an empty ping.
- **Health checks** — `AddTemporalHealthChecks()` registers an `IHealthCheck`
  that reports client liveness and per-queue poller counts.
- **`WorkerDiscovery`** — the auto-discovery engine, exposed for custom use.
- **Metrics** — a `System.Diagnostics.Metrics.Meter` plus interceptors that
  record high-level client operations and activity executions (with
  `workflow.id` and allowlisted baggage tags); the Core SDK's comprehensive
  `temporal_*` metrics are exported via the runtime's Prometheus/OpenTelemetry
  configuration.
- **Tracing** — wires the SDK's `TracingInterceptor` (OpenTelemetry
  `ActivitySource` spans) across the client and every worker, with optional
  baggage attributes.
- **`TemporalTestServerService`** — runs an in-process Temporal dev server when
  `Temporal:TestServer:Enabled` is `true`.
- **Shared `DataConverter`** — composes the enabled payload codecs (encryption +
  claim-check from `Temporal:DataConverter`) into a single `DataConverter` that
  is applied to the client and therefore every worker.

## Configuration

```json
{
  "Temporal": {
    "TargetHost": "localhost:7233",
    "Namespace": "default",
    "ApiKey": null,
    "Tls": null,
    "RpcRetry": {
      "InitialInterval": "00:00:00.100",
      "RandomizationFactor": 0.2,
      "Multiplier": 1.5,
      "MaxInterval": "00:00:05",
      "MaxElapsedTime": "00:00:10",
      "MaxRetries": 10
    },
    "KeepAlive": {
      "Interval": "00:00:30",
      "Timeout": "00:00:15"
    },
    "HttpConnectProxy": null,
    "DnsLoadBalancing": null,
    "GrpcCompression": {
      "Mode": "gzip"
    },
    "Metrics": {
      "Enabled": true,
      "MeterName": "Temporal.Hosting",
      "UseDefaultInterceptor": true,
      "BaggageTagKeys": [],
      "PrometheusBindAddress": null,
      "OpenTelemetryUrl": null
    },
    "Tracing": {
      "Enabled": true,
      "UseDefaultInterceptor": true,
      "BaggageTagKeys": []
    },
    "Logging": {
      "Enabled": true,
      "Category": "Temporalio.Core"
    },
    "TestServer": {
      "Enabled": true,
      "Port": 0
    },
    "ConnectionWait": {
      "Enabled": true,
      "Timeout": "00:01:00",
      "InitialDelay": "00:00:01",
      "MaxDelay": "00:00:15"
    },
    "Workers": {
      "my-task-queue": {
        "MaxConcurrentActivities": 20,
        "MaxConcurrentWorkflowTasks": 100,
        "GracefulShutdownTimeout": "00:00:30",
        "MaxCachedWorkflows": 1000,
        "Deployment": {
          "DeploymentName": "my-app",
          "BuildId": "1.0",
          "UseWorkerVersioning": true,
          "DefaultVersioningBehavior": "Pinned"
        }
      }
    },
    "DataConverter": {
      "Encryption": {
        "Enabled": true,
        "Key": "test-key-test-key-test-key-test!",
        "KeyId": "demo"
      },
      "ClaimCheck": {
        "Enabled": true,
        "ThresholdBytes": 1048576,
        "Directory": "claim-check"
      }
    },
    "ActivityOptions": {
      "Default": {
        "ScheduleToCloseTimeout": "00:05:00",
        "HeartbeatTimeout": "00:00:30"
      },
      "LocalDefault": {
        "ScheduleToCloseTimeout": "00:00:10"
      },
      "Presets": {
        "long-running": {
          "ScheduleToCloseTimeout": "00:30:00",
          "HeartbeatTimeout": "00:01:00"
        },
        "fast": {
          "StartToCloseTimeout": "00:00:05"
        }
      }
    },
    "Workflows": {
      "Id": { "Format": "{Type:s}-{Guid:N}", "ChildFormat": "{Type:s}-{Guid:N}-{Parent}" },
      "Default": {
        "TaskQueue": "orders-queue",
        "RunTimeout": "00:05:00",
        "TaskTimeout": "00:00:10",
        "IdConflictPolicy": "UseExisting"
      },
      "ByType": {
        "MoneyTransferWorkflow": {
          "TaskQueue": "payments-queue",
          "RunTimeout": "00:30:00"
        },
        "ChildWorkflow": {
          "ParentClosePolicy": "RequestCancel",
          "CancellationType": "TryCancel"
        }
      }
    },
    "WorkflowSettings": {
      "Default": { "batchSize": 10 },
      "ByType": {
        "BatchingWorkflow": { "batchSize": 100 }
      }
    },
    "HealthChecks": {
      "Enabled": true
    }
  }
}
```

Environment variables override the file (`Temporal__TargetHost`,
`Temporal__Metrics__Enabled`, ...).

## Usage

```csharp
using Kogoshvili.Temporal.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddTemporal(builder.Configuration)
    .AddTemporalWorker("my-task-queue")
    .AddDiscoveredTypes();

using var host = builder.Build();
await host.RunAsync();
```

### Registration: explicit by default, discovery opt-in

`AddTemporalWorker` registers no types by itself — add them explicitly on the
returned builder:

```csharp
builder.Services
    .AddTemporal(builder.Configuration)
    .AddTemporalWorker("sql-queue").AddSingletonActivities<SqlActivities>()
    .AddTemporalWorker("blob-queue").AddScopedActivities<BlobActivities>();
```

To opt a worker into convention-based auto-discovery, chain
`AddDiscoveredTypes()`:

```csharp
builder.Services.AddTemporalWorker("my-task-queue").AddDiscoveredTypes();
```

This scans the target assembly (default: the entry assembly) once at
registration time and registers every non-abstract `[Workflow]` class and every
class with an `[Activity]` method. Activity classes are registered as `scoped`
by default, and static classes as `static`. Override per type with the
`[ActivityLifetime]` attribute:

```csharp
[ActivityLifetime(ActivityLifetime.Singleton)]
public class MyActivities
{
    [Activity]
    public Task DoAsync() => Task.CompletedTask;
}
```

When you need a specific assembly (e.g. under `dotnet test`, where the entry
assembly is the test host), pass marker types instead:

```csharp
builder.Services.AddTemporalWorker("my-task-queue")
    .AddDiscoveredTypes(typeof(MyWorkflow), typeof(MyActivities));
```

Prefer explicit registration when a worker must run only a subset of an
assembly's types (e.g. separate queues for rate-limited vs. general
activities) — a worker should register only what it actually runs.

### Worker versioning

Version a worker from `Temporal:Workers:<task-queue>:Deployment` — no code is
required; a plain `AddTemporalWorker("my-task-queue")` applies the block
automatically:

```jsonc
"Workers": {
  "my-task-queue": {
    "Deployment": {
      "DeploymentName": "my-app",
      "BuildId": "1.0",              // "Version" is an alias for "BuildId"
      "UseWorkerVersioning": true,   // explicit opt-in; default false
      "DefaultVersioningBehavior": "Pinned"  // optional; omitted = Unspecified
    }
  }
}
```

`UseWorkerVersioning` is an explicit opt-in (defaults to `false`): a versioned
worker reports its deployment version on every poll but receives **no tasks**
until a Current (or Ramping) version is promoted server-side (e.g.
`temporal worker deployment set-current-version`). Omit the whole `Deployment`
block to keep a worker unversioned. To bypass config, pass the SDK's
`WorkerDeploymentOptions` directly to `AddTemporalWorker` — an explicit argument
wins over config.

### Per-queue worker configuration

Configure each worker from `Temporal:Workers:<task-queue>`. Every knob is
optional; unset values leave the SDK default untouched. An explicit `configure`
delegate passed to `AddTemporalWorker` overrides the appsettings value.

| Key | SDK property |
| --- | --- |
| `MaxConcurrentActivities` | `TemporalWorkerOptions.MaxConcurrentActivities` |
| `MaxConcurrentWorkflowTasks` | `TemporalWorkerOptions.MaxConcurrentWorkflowTasks` |
| `MaxConcurrentLocalActivities` | `TemporalWorkerOptions.MaxConcurrentLocalActivities` |
| `MaxConcurrentActivityTaskPolls` | `TemporalWorkerOptions.MaxConcurrentActivityTaskPolls` |
| `MaxConcurrentWorkflowTaskPolls` | `TemporalWorkerOptions.MaxConcurrentWorkflowTaskPolls` |
| `GracefulShutdownTimeout` | `TemporalWorkerOptions.GracefulShutdownTimeout` |
| `MaxCachedWorkflows` | `TemporalWorkerOptions.MaxCachedWorkflows` |
| `Namespace` | the namespace this worker polls (see "Multiple namespaces" below) |

### Multiple namespaces

A single shared `TemporalConnection` backs every namespace; clients are cheap,
lazily created, and cached per namespace. Declare the extra namespaces under
`Temporal:Namespaces`, and bind a worker to one via `AddTemporalWorker`:

```csharp
builder.Services
    .AddTemporal(builder.Configuration)
    .AddTemporalWorker("payments-queue", "payments").AddSingletonActivities<PaymentActivities>()
    .AddTemporalWorker("orders-queue", "orders").AddWorkflow<OrdersWorkflow>();
```

```jsonc
{
  "Temporal": {
    "Namespace": "default",        // the default/fallback namespace
    "Namespaces": [ "payments", "orders" ]
  }
}
```

A worker's namespace resolves in this order: the explicit `AddTemporalWorker`
argument, then `Temporal:Workers:<task-queue>:Namespace`, then the default
`Temporal:Namespace`. Resolve a namespace-scoped client at runtime through
`ITemporalClientFactory` (the default `ITemporalClient` is the default
namespace's client):

```csharp
var payments = clientFactory.Get("payments");
```

To bypass config entirely, hand `AddTemporal` a pre-built SDK client (or a
connection, or a `Func<IServiceProvider, ITemporalClient>` factory):

```csharp
builder.Services.AddTemporal(TemporalClient.CreateLazy(new("localhost:7233") { Namespace = "custom" }));
```

### Activity-options presets

`Temporal:ActivityOptions` seeds default and named activity-options presets
(timeouts, retry policy, cancellation type, task queue). A single preset maps to
both a regular `ActivityOptions` and a `LocalActivityOptions` — regular-only
fields (`HeartbeatTimeout`, `TaskQueue`) and the local-only `LocalRetryThreshold`
apply only where supported. Workflows resolve them through the static
`ActivityOps` facade — workflows run in the replay sandbox and cannot use DI, so
the registry is populated once at `AddTemporal` time and only read during
execution:

```csharp
[Workflow]
public sealed class MyWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string name)
    {
        await ActivityOps.ExecuteAsync(() => MyActivities.DoIt(name));             // default preset
        await ActivityOps.ExecuteAsync(() => MyActivities.DoIt(name), "long-running"); // named preset
        await ActivityOps.ExecuteAsync(() => MyActivities.DoIt(name),              // explicit options
            new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(30) });

        await ActivityOps.ExecuteLocalAsync(() => MyActivities.LocalDoIt(name));   // local default
        await ActivityOps.ExecuteLocalAsync(() => MyActivities.LocalDoIt(name), "fast");

        return "done";
    }
}
```

`ActivityOps` mirrors `Workflow.ExecuteActivityAsync` /
`Workflow.ExecuteLocalActivityAsync` with no reduction in surface: pass a preset
name, omit it for the default, or pass an explicit `ActivityOptions` /
`LocalActivityOptions`. The regular and local defaults are independent —
`Temporal:ActivityOptions:Default` and `Temporal:ActivityOptions:LocalDefault`
respectively — while the `Presets` map is shared by both.

Each preset must set `ScheduleToCloseTimeout` or `StartToCloseTimeout` (the
SDK's own rule); unset properties leave the SDK defaults, and an unset `Retry`
means "retry forever". Presets are captured at startup and are **not**
live-reloaded, to keep workflow replay deterministic. `ActivityOptionsRegistry`
remains available for cases that need the raw options object (`Get`, `GetLocal`,
`GetDefault`, `GetLocalDefault`, `Resolve`, `ResolveLocal`); all return clones so
callers may mutate the result safely.

If you don't configure a default preset, a **built-in default** is used so
`ActivityOps.ExecuteAsync(call)` / `ActivityOps.ExecuteLocalAsync(call)` work
out of the box: regular activities default to a five-minute
`ScheduleToCloseTimeout`, local activities to ten seconds. Override them with
`Temporal:ActivityOptions:Default` and `Temporal:ActivityOptions:LocalDefault`,
or read the built-ins directly via `ActivityOptionsRegistry.BuiltInDefault` /
`ActivityOptionsRegistry.BuiltInLocalDefault`.

### Workflow-options presets and ID conventions

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

### Child-workflow ops

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

### Workflow settings

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

### Schedules

Temporal Schedules start a workflow on a recurring calendar/interval/cron
cadence, server-side. The SDK gives you `CreateScheduleAsync` (which throws
`ScheduleAlreadyRunningException` on a duplicate ID) and `GetScheduleHandle`
(which is lazy and does not validate existence) — no idempotent "get-or-create".
This library adds idempotent registration, declarable from config or code.

Config-driven schedules are declared under `Temporal:Schedules`, keyed by
schedule ID, and registered once at startup by `TemporalScheduleRegistrar` (after
the connection waiter). The `Action` mirrors the SDK's
`ScheduleActionStartWorkflow` and the `Spec`/`Policy`/`State` mirror
`ScheduleSpec`/`SchedulePolicy`/`ScheduleState` directly — no shorthand is
invented (calendar specs are the SDK's `{ Start, End, Step }` ranges; cron
strings pass through as `Spec:Cron`):

```json
{
  "Temporal": {
    "Schedules": {
      "nightly-cleanup": {
        "Action": {
          "Workflow": "CleanupWorkflow",
          "TaskQueue": "cleanup",
          "WorkflowId": "{Type:s}-cleanup",
          "RunTimeout": "00:05:00"
        },
        "Spec": {
          "Cron": [ "0 0 * * *" ],
          "TimeZoneName": "UTC"
        },
        "Policy": {
          "Overlap": "BufferAll",
          "CatchupWindow": "01:00:00",
          "PauseOnFailure": true
        },
        "State": { "Paused": false },
        "TriggerImmediately": false,
        "Reconcile": false
      }
    }
  }
}
```

`Reconcile` (default `false`) controls what happens when the schedule already
exists at startup:

- `false` — pure get-or-create: leave an existing schedule untouched.
- `true` — drive it toward the declared definition (`UpdateAsync` on drift).

Workflow **arguments are code-only**: `Temporal:Schedules` cannot express typed
workflow input. Use `AddTemporalSchedule` (which accepts a built `Schedule`, or a
typed workflow invocation) for schedules that pass arguments, and `IScheduleOps`
for imperative control at runtime:

```csharp
builder.Services.AddTemporal()
    // Typed schedule with arguments, declared in code:
    .AddTemporalSchedule(
        "nightly-cleanup",
        (CleanupWorkflow wf) => wf.RunAsync(environment: "prod"),
        new WorkflowOptions(id: "cleanup-prod", taskQueue: "cleanup"),
        new ScheduleSpec { CronExpressions = new[] { "0 0 * * *" } },
        reconcile: true);
```

`IScheduleOps` is the client-side facade (injected singleton, like
`IWorkflowOps`). Its `RegisterAsync` is the idempotent core (`reconcile: false`
creates-or-no-ops, `reconcile: true` also updates on drift), and the rest is a
full pass-through to `ScheduleHandle`/`ITemporalClient`:

```csharp
var handle = await scheduleOps.RegisterAsync(
    "nightly-cleanup", schedule, reconcile: true);

await scheduleOps.PauseAsync("nightly-cleanup", note: "maintenance");
await scheduleOps.TriggerAsync("nightly-cleanup");
await foreach (var schedule in scheduleOps.ListAsync()) { /* ... */ }
```

### Search attributes

Search attributes are custom, typed key/value metadata you attach to workflow
executions so they can be queried. Before you can set or query one, it must
exist on the server's namespace — normally an ops step
(`temporal operator search-attribute create ...`). This library bootstraps them:
declared attributes are registered once at startup by `SearchAttributeRegistrar`
(after the connection waiter), idempotently, across the default namespace and
every `Temporal:Namespaces` entry.

Attributes are declared under `Temporal:SearchAttributes`, keyed by name with an
indexed value type:

```json
{
  "Temporal": {
    "SearchAttributes": {
      "Enabled": true,
      "FailOnConflict": false,
      "Attributes": {
        "CustomerId": { "Type": "Keyword" },
        "Amount":     { "Type": "Double" }
      }
    }
  }
}
```

- `Enabled` (default `true`) — turn off registration where an environment lacks
  operator permission.
- `FailOnConflict` (default `false`) — when an attribute already exists with a
  different type, fail startup (`true`) rather than log a warning (`false`).
- `Attributes` — name → `Type` (`Keyword`, `Text`, `Int`, `Double`, `Bool`,
  `Datetime`, `KeywordList`).

Registration is **add-only and idempotent**: missing attributes are added, and
declared attributes are never removed.

This only bootstraps the *keys*. Setting *values* is done by your workflow code
through the SDK, either at start via `WorkflowOptions.TypedSearchAttributes` (or
`ChildWorkflowOptions.TypedSearchAttributes`) or during execution via
`Workflow.UpsertTypedSearchAttributes(...)`.

### Connection retry and startup wait

`RpcRetry` maps onto the SDK's connection-level `RpcRetryOptions`, controlling
the retry policy for server calls (`InitialInterval`, `Multiplier`, `MaxRetries`,
and so on). Set it to `null` (the default) to keep the SDK defaults.

`ConnectionWait` makes the starter wait for the server to be reachable before
workers poll: on startup a hosted service connects the shared lazy client,
retrying with exponential backoff (`InitialDelay` → `MaxDelay`) until success or
`Timeout` (set `Timeout` to `null` to retry indefinitely). It is enabled by
default and ignored when the test server is used.

### Configuration reload

Options are bound through `IOptionsMonitor<TemporalOptions>`, so the options
**value** reflects `appsettings.json` changes, and
`IValidateOptions<TemporalOptions>` re-validates on every reload — an invalid
new value is rejected with `OptionsValidationException` on next access.

However, reload is **validate-only**, not *apply*: the client connection,
workers, codecs, and runtime are constructed once from a snapshot at
registration/startup and are **not** reconfigured when the value changes. The
Temporal .NET SDK treats connection and worker options as snapshots too — the
hosted `TemporalWorkerService` clones its options once at construction and never
subscribes to changes, and the only runtime-mutable connection properties are
`ApiKey`, `RpcMetadata`, and `RpcBinaryMetadata`. Exceptions:

- `TemporalHealthCheck` reads the current value per invocation, so
  `HealthChecks:Enabled` toggles live.
- `ActivityOptions` presets are seeded once and deliberately not live-reloaded
  (see below).

True live reload (reconnecting the client and restarting workers on change) is
not implemented; see the repository TODO.

### Connection transport options

Beyond `RpcRetry`, the remaining connection-level SDK knobs are exposed from
configuration (each `null` = leave the SDK default untouched):

| Key | SDK property |
| --- | --- |
| `Temporal:KeepAlive` (`Interval`, `Timeout`) | `KeepAliveOptions` |
| `Temporal:HttpConnectProxy` (`TargetHost`, `Username`, `Password`) | `HttpConnectProxyOptions` |
| `Temporal:DnsLoadBalancing` (`ResolutionInterval`) | `DnsLoadBalancingOptions` |
| `Temporal:GrpcCompression:Mode` (`"gzip"` or `"none"`) | `GrpcCompression` |

### Health checks

`AddTemporalHealthChecks()` registers an `IHealthCheck` (named `temporal`) that
verifies the shared client connection is serving and, for every task queue
registered via `AddTemporalWorker`, that the queue has at least one poller
(a connected worker). It reports `Unhealthy` when the server is unreachable,
`Degraded` when a queue has no pollers, and `Healthy` otherwise, with per-queue
poller counts in the result data:

```csharp
builder.Services.AddTemporalHealthChecks();
```

```csharp
app.MapHealthChecks("/health"); // ASP.NET Core endpoint
```

Disable it at runtime (without removing the registration) via
`Temporal:HealthChecks:Enabled = false`. Poller discovery uses the raw
`DescribeTaskQueue` RPC; the `ReportPollers` request field is marked obsolete
in SDK 1.18 but remains the only way to observe poller liveness.

### TLS sources

`Temporal:Tls:Source` selects where client certificates come from:

- **`file`** (default) — PEM files at `Tls:ClientCertPath`,
  `Tls:ClientPrivateKeyPath`, and `Tls:ServerRootCACertPath`.
- **`environment`** — inline `Tls:ClientCert` / `Tls:ClientPrivateKey` /
  `Tls:ServerRootCACert` strings (base64 or raw PEM), typically injected as
  environment variables (`Temporal__Tls__ClientCert=…`).
- **`azureKeyVault`** / **`awsSecretsManager`** — fetched asynchronously at
  startup by `TemporalCertificateLoader` before the connection waiter and
  workers start. Register the source from `Kogoshvili.Temporal.Cloud` and
  configure its section:

```csharp
builder.Services.AddAzureKeyVaultCertificateSource(); // or AddAwsSecretsManagerCertificateSource()
```

```json
{
  "Temporal": {
    "Tls": {
      "Source": "azureKeyVault",
      "AzureKeyVault": {
        "VaultUri": "https://my-vault.vault.azure.net",
        "CertificateName": "temporal-client"
      }
    }
  }
}
```

Azure Key Vault stores certificates as PFX; `AzureKeyVaultCertificateSource`
converts them to the PEM form the SDK requires. The `file` and `environment`
sources are resolved synchronously by `ClientOptionsFactory`, so they also work
with the `temporal-sharp` CLI and the testing harness.

### Metrics

`Metrics:Enabled` registers a `System.Diagnostics.Metrics.Meter` and a built-in
interceptor that records high-level client operations and activity executions:

| Metric | Tags |
|---|---|
| `temporal.client.workflow.start.count/.duration` | `workflow.type`, `namespace`, `error` |
| `temporal.client.workflow.signal.count/.duration` | `workflow.id`, `signal`, `namespace`, `error` |
| `temporal.client.workflow.query.count/.duration` | `workflow.id`, `query`, `namespace`, `error` |
| `temporal.client.workflow.update.count/.duration` | `workflow.id`, `update`, `namespace`, `error` |
| `temporal.client.workflow.cancel.count/.duration` | `workflow.id`, `namespace`, `error` |
| `temporal.client.workflow.terminate.count/.duration` | `workflow.id`, `namespace`, `error` |
| `temporal.worker.activity.execution.count/.duration` | `activity.type`, `workflow.id`, `workflow.type`, `task.queue`, `namespace`, `error` |

The Comprehensive Core SDK metrics (client RPC counts/latency, workflow
completion, activity latency, pollers, caches, slots — the `temporal_*` set)
are exported through the runtime. Set either `Metrics:PrometheusBindAddress`
(e.g. `0.0.0.0:9000`) or `Metrics:OpenTelemetryUrl`
(e.g. `http://localhost:4317`) to enable them.

The custom metrics are recorded on the `.NET` `Meter` named by
`Metrics:MeterName`. To export them, subscribe a listener to that meter, e.g.
with OpenTelemetry:

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m.AddMeter("Temporal.Hosting").AddOtlpExporter());
```

`Metrics:UseDefaultInterceptor` (default `true`) disables the built-in
interceptor while keeping the meter, so you can record your own metrics via the
SDK's `Interceptors = [...]` option.

`Metrics:BaggageTagKeys` is an explicit allowlist of OpenTelemetry baggage keys
whose values are attached as `baggage.<key>` tags. It is empty by default to
avoid leaking arbitrary baggage into metric dimensions. On the worker side these
tags only appear when tracing is also enabled, because baggage is propagated and
restored by the tracing interceptor.

### Tracing

`Tracing:Enabled` wires the SDK's `TracingInterceptor` (from
`Temporalio.Extensions.OpenTelemetry`) onto the client; because it is also a
worker interceptor, it applies to every worker automatically. Spans are created
via `ActivitySource` for client calls, workflows, and activities, with W3C trace
context propagated through Temporal headers.

Spans are only emitted when something listens to the sources, so register them
with your tracer provider:

```csharp
using Temporalio.Extensions.OpenTelemetry;

builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddSource(
            TracingInterceptor.ClientSource.Name,
            TracingInterceptor.WorkflowsSource.Name,
            TracingInterceptor.ActivitiesSource.Name)
        .AddConsoleExporter());
```

`Tracing:UseDefaultInterceptor` (default `true`) disables the built-in
interceptor, letting you install your own via `Interceptors = [...]`.
`Tracing:BaggageTagKeys` attaches allowlisted baggage entries as
`baggage.<key>` span attributes (client, workflow, and activity spans).

### Core log forwarding

`Logging:Enabled` forwards the SDK runtime's Core (Rust bridge) logs into the
application's `ILogger` pipeline under `Logging:Category` (default
`Temporalio.Core`), so they respect the usual `Logging:LogLevel` filters:

```json
{
  "Logging": {
    "LogLevel": {
      "Temporalio.Core": "Debug"
    }
  }
}
```

This requires an `ILoggerFactory` in the container (present by default in a
generic host) and creates a dedicated `TemporalRuntime`. It is opt-in because
constructing a runtime spawns its own Core thread pool.

### Payload codecs (DataConverter)

`DataConverter` builds a shared `DataConverter` from the enabled codecs and
applies it to the client (workers inherit it, so client and workers always
encode consistently). Both codecs are opt-in:

- **`DataConverter:Encryption`** — AES-GCM encrypts every payload before it is
  sent to the server, with a key id for rotation. `Key` is an ASCII string of 16,
  24, or 32 bytes (in production, source it from your KMS).
- **`DataConverter:ClaimCheck`** — offloads payloads larger than
  `ThresholdBytes` to a filesystem store, leaving a small reference in the
  workflow history. Azure Blob and S3 stores ship in
  `Kogoshvili.Temporal.Cloud`.

The composed codec is registered as a singleton `IPayloadCodec`, so a
[`Kogoshvili.Temporal.CodecServer`](https://www.nuget.org/packages/Kogoshvili.Temporal.CodecServer)
hosted in the same app can expose `/encode` and `/decode` over HTTP for the
Temporal Web UI and CLI using the exact same codec.

Not affiliated with or endorsed by Temporal Technologies.
