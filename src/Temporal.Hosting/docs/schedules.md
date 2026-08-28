# Schedules

Temporal Schedules start a workflow on a recurring calendar/interval/cron
cadence, server-side. This library registers them idempotently at startup —
declared from config under `Temporal:Schedules` or from code via
`AddTemporalSchedule` — and exposes `IScheduleOps` for imperative control.

## Minimal setup

A config-driven schedule is a pure `appsettings.json` block under
`Temporal:Schedules`, keyed by schedule ID. `Action` (with `Workflow`,
`TaskQueue`, and `WorkflowId`, all required) names the workflow to start and
`Spec` says when. `TemporalScheduleRegistrar` creates it once at startup, after
the connection waiter, and no-ops if it already exists. No C# is required:

```json
{
  "Temporal": {
    "Schedules": {
      "nightly-cleanup": {
        "Action": {
          "Workflow": "CleanupWorkflow",
          "TaskQueue": "cleanup",
          "WorkflowId": "{Type:s}-cleanup"
        },
        "Spec": {
          "Cron": [ "0 0 * * *" ]
        }
      }
    }
  }
}
```

## Configuration

The `Action` mirrors the SDK's `ScheduleActionStartWorkflow`, the `Spec`
mirrors `ScheduleSpec`, `Policy` mirrors `SchedulePolicy`, and `State` mirrors
`ScheduleState` — no shorthand is introduced. In addition to a `Cron`
expression, `Spec` accepts calendar specs (each field a list of inclusive
`{ Start, End, Step }` ranges, where `End` defaults to `Start` and `Step` to
`1`) or interval specs (`Every` is required):

```json
{
  "Temporal": {
    "Schedules": {
      "nightly-cleanup": {
        "Action": {
          "Workflow": "CleanupWorkflow",
          "TaskQueue": "cleanup",
          "WorkflowId": "{Type:s}-cleanup"
        },
        "Spec": {
          "Calendars": [
            { "Hour": [ { "Start": 2 } ], "Minute": [ { "Start": 0 } ] }
          ],
          "TimeZoneName": "UTC"
        },
        "Policy": {
          "Overlap": "BufferAll",
          "CatchupWindow": "01:00:00",
          "PauseOnFailure": true
        },
        "State": {
          "Paused": false,
          "LimitedActions": true,
          "RemainingActions": 100
        },
        "TriggerImmediately": false,
        "Reconcile": false
      }
    }
  }
}
```

- `Action.Workflow` / `Action.TaskQueue` / `Action.WorkflowId` are required;
  startup fails if any is missing. `WorkflowId` supports the `{Type}`/`{Type:s}`,
  `{Queue}`, and `{Guid}` placeholders.
- `Spec` accepts `Calendars`, `Intervals` (`Every` + optional `Offset`), `Cron`,
  `Skip`, `StartAt`, `EndAt`, `Jitter`, and `TimeZoneName` (IANA, e.g.
  `US/Central`).
- `Policy` defaults to `Overlap: Skip`, `CatchupWindow: 365d`, and
  `PauseOnFailure: false` when omitted.
- `State` carries `Note`, `Paused`, `LimitedActions`, and `RemainingActions`.
- `TriggerImmediately` fires one action on creation; it is create-time only and
  not persisted.

`Reconcile` (default `false`) controls the idempotency behavior when the
schedule already exists: `false` leaves it untouched (pure get-or-create);
`true` describes and updates it toward the declared definition on drift.

## Full configuration

Config-driven schedules cannot pass typed workflow arguments — `Action` is a
workflow type name, not a typed invocation. For schedules that take arguments,
declare them in code with `AddTemporalSchedule`, which accepts a fully-built
`Schedule` or a typed workflow invocation:

```csharp
builder.Services.AddTemporal(builder.Configuration)
    .AddTemporalSchedule(
        "nightly-cleanup",
        (CleanupWorkflow wf) => wf.RunAsync(environment: "prod"),
        new WorkflowOptions(id: "cleanup-prod", taskQueue: "cleanup"),
        new ScheduleSpec { CronExpressions = new[] { "0 0 * * *" } },
        reconcile: true);
```

The remaining `Action` knobs — `RunTimeout`, `TaskTimeout`, `ExecutionTimeout`,
`Retry`, `StaticSummary`, `StaticDetails` — and the `Spec` calendar `Skip`
range, `StartAt`/`EndAt` bounds, and `Jitter` are all expressible in config as
the same-named properties shown above. `Policy` and `State` are optional in
both config and code and fall back to the SDK defaults when unset.

`IScheduleOps` (injected singleton) is the client-side facade. `RegisterAsync`
is the idempotent core — it creates the schedule, catches
`ScheduleAlreadyRunningException`, and, with `reconcile: true`, updates an
existing schedule toward the desired definition. The rest are pass-throughs to
`ScheduleHandle` / `ITemporalClient`:

```csharp
var handle = await scheduleOps.RegisterAsync(
    "nightly-cleanup", schedule, reconcile: true);

await scheduleOps.PauseAsync("nightly-cleanup", note: "maintenance");
await scheduleOps.UnpauseAsync("nightly-cleanup");
await scheduleOps.TriggerAsync("nightly-cleanup");
await scheduleOps.BackfillAsync("nightly-cleanup", backfills);
await scheduleOps.UpdateAsync("nightly-cleanup", input => new ScheduleUpdate(schedule));
var description = await scheduleOps.DescribeAsync("nightly-cleanup");
await foreach (var schedule in scheduleOps.ListAsync()) { /* ... */ }
await scheduleOps.DeleteAsync("nightly-cleanup");
```

`RegisterAsync` also offers typed overloads (`Expression<Func<TWorkflow, ...>>`
or a workflow type name + args) that build the schedule internally, matching the
`AddTemporalSchedule` overloads. Both the config-driven registrar and the
code-driven registrations are applied in the same startup pass, so a schedule
declared both ways is registered idempotently regardless of source.
