# Worker registration

`AddTemporalWorker(taskQueue)` registers a hosted worker that polls a task queue
and runs workflows and activities on it. By default it runs nothing — you
register types explicitly, or opt into convention-based auto-discovery.

## Minimal setup

The smallest worker needs a `Temporal` section (at minimum `TargetHost`), a task
queue, and types to run. `AddTemporalWorker` registers no types by itself;
`AddDiscoveredTypes` is the opt-in auto-discovery that scans the entry assembly
and registers every `[Workflow]`/`[Activity]` type it finds:

```csharp
builder.Services
    .AddTemporal(builder.Configuration)
    .AddTemporalWorker("my-task-queue")
    .AddDiscoveredTypes();
```

```json
{
  "Temporal": {
    "TargetHost": "localhost:7233"
  }
}
```

## Configuration

### Per-queue tuning

Tune a worker from `Temporal:Workers:<task-queue>`. Every knob is optional; an
unset value leaves the SDK default untouched:

```jsonc
{
  "Temporal": {
    "Workers": {
      "my-task-queue": {
        "MaxConcurrentActivities": 20,
        "MaxConcurrentWorkflowTasks": 100,
        "MaxConcurrentLocalActivities": 100,
        "MaxConcurrentActivityTaskPolls": 2,
        "MaxConcurrentWorkflowTaskPolls": 2,
        "GracefulShutdownTimeout": "00:00:30",
        "MaxCachedWorkflows": 1000
      }
    }
  }
}
```

The block applies automatically to a plain `AddTemporalWorker("my-task-queue")`
call — no code change required.

### Worker versioning

Opt a worker into Worker Versioning from
`Temporal:Workers:<task-queue>:Deployment`. No code is required; a plain
`AddTemporalWorker("my-task-queue")` applies the block automatically:

```jsonc
{
  "Temporal": {
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
  }
}
```

`UseWorkerVersioning` defaults to `false`. When enabled, a versioned worker
reports its deployment version on every poll but receives **no tasks** until a
Current (or Ramping) version is promoted server-side (e.g.
`temporal worker deployment set-current-version`). Omit the whole `Deployment`
block to keep a worker unversioned. When `BuildId` and `Version` are both set,
`BuildId` wins.

### Multiple namespaces

A single shared connection backs every namespace; namespace-scoped clients are
cheap, lazily created, and cached per namespace. Declare the extra namespaces
under `Temporal:Namespaces`, and bind a worker to one via the `ns` argument:

```jsonc
{
  "Temporal": {
    "Namespace": "default",          // the default/fallback namespace
    "Namespaces": [ "payments", "orders" ]
  }
}
```

```csharp
builder.Services
    .AddTemporal(builder.Configuration)
    .AddTemporalWorker("payments-queue", "payments").AddSingletonActivities<PaymentActivities>()
    .AddTemporalWorker("orders-queue", "orders").AddWorkflow<OrdersWorkflow>();
```

A worker's namespace resolves in this order: the explicit `AddTemporalWorker`
argument, then `Temporal:Workers:<task-queue>:Namespace`, then the default
`Temporal:Namespace`.

## Full configuration

### Explicit registration

Prefer explicit registration when a worker must run only a subset of an
assembly's types (e.g. separate queues for rate-limited vs. general activities).
This is code-only — there is no config key; chain the type-registration methods
on the builder returned by `AddTemporalWorker`:

```csharp
builder.Services
    .AddTemporal(builder.Configuration)
    .AddTemporalWorker("sql-queue").AddSingletonActivities<SqlActivities>()
    .AddTemporalWorker("blob-queue").AddScopedActivities<BlobActivities>();
```

Available methods (also accept `Type` in place of the generic):

| Method | Lifetime |
| --- | --- |
| `AddWorkflow<T>()` | workflow class |
| `AddSingletonActivities<T>()` | one instance, reused |
| `AddScopedActivities<T>()` | new instance per activity attempt |
| `AddTransientActivities<T>()` | new instance per resolution |
| `AddStaticActivities<T>()` | static methods, no instance |

### Activity lifetimes under auto-discovery

`AddDiscoveredTypes` scans the entry assembly (or, when absent, the calling
assembly) once at registration time. It registers every non-abstract
`[Workflow]` class and every class with an `[Activity]` method. Activity
lifetimes are assigned by convention: static classes become `Static`, instance
classes become `Scoped`. Override per type with the attribute:

```csharp
[ActivityLifetime(ActivityLifetime.Singleton)]
public class MyActivities
{
    [Activity]
    public Task DoAsync() => Task.CompletedTask;
}
```

When the entry assembly is not the worker assembly (e.g. under `dotnet test`,
where the entry assembly is the test host), pass marker types instead so
discovery scans the right assemblies:

```csharp
builder.Services.AddTemporalWorker("my-task-queue")
    .AddDiscoveredTypes(typeof(MyWorkflow), typeof(MyActivities));
```

### Precedence

- **Namespace** — explicit `ns` argument > `Temporal:Workers:<queue>:Namespace` >
  `Temporal:Namespace`.
- **Per-queue tuning** — an explicit `configure` delegate passed to
  `AddTemporalWorker` wins over `Temporal:Workers:<queue>` values.
- **Deployment** — an explicit `WorkerDeploymentOptions` argument passed to
  `AddTemporalWorker` wins over the `Temporal:Workers:<queue>:Deployment` block.
  Pass the SDK's `WorkerDeploymentOptions` to bypass config entirely:

```csharp
builder.Services.AddTemporalWorker(
    "my-task-queue",
    ns: null,
    new WorkerDeploymentOptions(
        new WorkerDeploymentVersion("my-app", "1.0"),
        useWorkerVersioning: true));
```

Deployment identity (the task queue + version pair) is resolved eagerly at
registration time and cannot be changed later via `ConfigureOptions`.

### Resolving namespace-scoped clients

Resolve a namespace-scoped client at runtime through `ITemporalClientFactory`;
the injected default `ITemporalClient` is the default namespace's client:

```csharp
var payments = clientFactory.Get("payments");
```

To bypass config entirely, hand `AddTemporal` a pre-built SDK client (or a
connection, or a `Func<IServiceProvider, ITemporalClient>` factory):

```csharp
builder.Services.AddTemporal(TemporalClient.CreateLazy(new("localhost:7233") { Namespace = "custom" }));
```
