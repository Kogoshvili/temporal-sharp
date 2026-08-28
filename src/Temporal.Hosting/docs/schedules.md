# Schedules

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
