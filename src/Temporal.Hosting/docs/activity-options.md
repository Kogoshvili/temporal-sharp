# Activity-options presets

`Temporal:ActivityOptions` seeds default and named activity-options presets
(timeouts, retry policy, cancellation type, task queue), resolved from inside
workflows through the static `ActivityOps` facade. A single preset maps to both
a regular `ActivityOptions` and a `LocalActivityOptions`.

## Minimal setup

`ActivityOps` needs no configuration at all: with an empty `appsettings.json`,
every preset resolves to a built-in default, so `ExecuteAsync(call)` /
`ExecuteLocalAsync(call)` work out of the box.

```csharp
using Kogoshvili.Temporal.Hosting;
using Temporalio.Workflows;

[Workflow]
public sealed class GreetingWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string name)
    {
        var regular = await ActivityOps.ExecuteAsync(() => StaticActivities.Greet(name));
        var local = await ActivityOps.ExecuteLocalAsync(() => StaticActivities.Greet(name));

        return regular;
    }
}
```

The built-in defaults are a five-minute `ScheduleToCloseTimeout` for regular
activities and ten seconds for local activities, both with no retry cap. They
live on `ActivityOptionsRegistry.BuiltInDefault` /
`ActivityOptionsRegistry.BuiltInLocalDefault`.

`ActivityOps` mirrors `Workflow.ExecuteActivityAsync` /
`Workflow.ExecuteLocalActivityAsync` with no reduction in surface. Every call
takes one of three preset forms:

```csharp
// Default preset (the built-in default, or Temporal:ActivityOptions:Default).
await ActivityOps.ExecuteAsync(() => StaticActivities.Greet(name));

// Named preset (Temporal:ActivityOptions:Presets:<name>).
await ActivityOps.ExecuteAsync(() => StaticActivities.Greet(name), "long-running");

// Explicit options, bypassing presets entirely.
await ActivityOps.ExecuteAsync(
    () => StaticActivities.Greet(name),
    new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(30) });
```

`ExecuteLocalAsync` takes the same three forms, with `LocalActivityOptions` in
place of `ActivityOptions`.

## Configuration

Presets are declared under `Temporal:ActivityOptions`. The smallest config is a
single default preset, overriding the built-in five-minute regular default with
a heartbeat (so the activity stays cancellable and can report progress):

```json
{
  "Temporal": {
    "ActivityOptions": {
      "Default": {
        "ScheduleToCloseTimeout": "00:05:00",
        "HeartbeatTimeout": "00:00:30"
      }
    }
  }
}
```

`Default` applies to regular activities and `LocalDefault` to local activities,
so each can carry type-specific fields independently. A richer config adds a
local default and named presets:

```json
{
  "Temporal": {
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
          "HeartbeatTimeout": "00:01:00",
          "Retry": {
            "InitialInterval": "00:00:01",
            "BackoffCoefficient": 2.0,
            "MaximumAttempts": 5
          }
        },
        "fast": {
          "StartToCloseTimeout": "00:00:05"
        }
      }
    }
  }
}
```

A single named preset serves both `ExecuteAsync(call, "name")` and
`ExecuteLocalAsync(call, "name")`: regular-only fields (`HeartbeatTimeout`,
`TaskQueue`) are ignored by the local form, and the local-only
`LocalRetryThreshold` is ignored by the regular form. Each preset must set
`ScheduleToCloseTimeout` or `StartToCloseTimeout` (the SDK's own rule); a preset
that sets neither throws at startup. Unset properties leave the SDK default, and
an unset `Retry` means "retry forever".

Presets are bound through the `ActivityOptionsPreset` config class, and `Retry`
through `RetryPolicyOptions`.

## Full configuration

Every `ActivityOptionsPreset` field, the SDK property it maps to, and its scope:

| Field | SDK property | Scope |
| --- | --- | --- |
| `ScheduleToCloseTimeout` | `ScheduleToCloseTimeout` | regular + local |
| `ScheduleToStartTimeout` | `ScheduleToStartTimeout` | regular + local |
| `StartToCloseTimeout` | `StartToCloseTimeout` | regular + local |
| `HeartbeatTimeout` | `HeartbeatTimeout` | regular only |
| `CancellationType` | `CancellationType` | regular + local |
| `TaskQueue` | `TaskQueue` | regular only |
| `Retry` | `RetryPolicy` | regular + local |
| `LocalRetryThreshold` | `LocalRetryThreshold` | local only |
| `ActivityId` | `ActivityId` | regular + local |
| `Summary` | `Summary` | regular + local |

`CancellationType` is the SDK's `ActivityCancellationType`:
`"TryCancel"` (the SDK default), `"WaitCancellationCompleted"`, or `"Abandon"`.
`TaskQueue` unset runs the activity on the workflow's own task queue. Duration
fields are time-span strings (`"00:05:00"`).

The `Retry` block mirrors the SDK's `RetryPolicy`:

| Field | SDK default |
| --- | --- |
| `InitialInterval` | `00:00:01` |
| `BackoffCoefficient` | `2.0` |
| `MaximumInterval` | none |
| `MaximumAttempts` | `0` (unlimited) |
| `NonRetryableErrorTypes` | empty |

Presets are config-only: they are seeded once from `Temporal:ActivityOptions` at
`AddTemporal` time (bound into `TemporalActivityOptions`), and there is no code
API to register additional presets. For per-call control that is not worth a
named preset, pass an explicit `ActivityOptions` / `LocalActivityOptions` to the
call.

Precedence (lowest to highest): SDK defaults → built-in default → the configured
`Default`/`LocalDefault`/`Presets` entry → explicit `ActivityOptions` /
`LocalActivityOptions` passed to the call.

Presets are captured once at `AddTemporal` time and are **not** live-reloaded, so
a workflow replaying later resolves the same options it saw on its first run.
`ActivityOptionsRegistry` remains available for code that needs the raw options
object: `Get(name)`, `GetLocal(name)`, `GetDefault()`, `GetLocalDefault()`,
`Resolve(name)` / `ResolveLocal(name)` (default when `null`), `TryGet`, and
`Names`. Every accessor returns a clone, so callers may mutate the result safely.
