# Health checks

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
