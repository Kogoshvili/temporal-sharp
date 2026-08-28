# Worker registration

## Registration: explicit by default, discovery opt-in

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

## Worker versioning

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

## Per-queue worker configuration

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

## Multiple namespaces

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
