# Health checks

`AddTemporalHealthChecks()` registers an `IHealthCheck` (named `temporal`) that
verifies the shared client connection is serving and that every registered task
queue has at least one poller (a connected worker).

## Minimal setup

One registration line plus a mapped endpoint is all that is required:

```csharp
builder.Services.AddTemporalHealthChecks();
```

```csharp
app.MapHealthChecks("/health");
```

The check reports `Healthy` when the server is reachable and every queue has a
poller, `Degraded` when the server is serving but a queue has no poller (worker
not connected), and `Unhealthy` when the server is unreachable or not serving.
Per-queue poller counts are included in the result data (keyed
`<queue>:pollers`, with the string `"error"` when a queue could not be
described).

## Configuration

`AddTemporalHealthChecks()` is opt-in: without the call, no health check is
registered. The check itself has a single configurable knob, bound from
`Temporal:HealthChecks`:

```json
{
  "Temporal": {
    "HealthChecks": {
      "Enabled": true
    }
  }
}
```

`Enabled` defaults to `true`. Set it to `false` to switch the check off without
removing the registration — it then reports `Healthy` without contacting the
server:

```jsonc
{
  "Temporal": {
    "HealthChecks": {
      "Enabled": false
    }
  }
}
```

Unlike most other Temporal options (which are snapshotted at startup),
`TemporalHealthCheck` reads the current value per invocation via
`IOptionsMonitor`, so `HealthChecks:Enabled` toggles live on config reload.

## Full configuration

The queue list is derived from the worker registry — every task queue
registered through `AddTemporalWorker` (explicit or auto-discovered) is checked;
there is no separate per-queue config for the health check.

Poller discovery uses the raw `DescribeTaskQueue` RPC with `ReportPollers =
true`. `ReportPollers` is marked obsolete as part of the SDK 1.18.0
ENHANCED-mode deprecation, but it remains the only supported mechanism for
observing poller liveness in DEFAULT mode, so the check relies on it.
