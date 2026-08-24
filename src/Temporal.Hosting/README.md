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
- **`AddTemporalWorker`** — registers a hosted worker and auto-discovers
  `[Workflow]` and `[Activity]` types by convention.
- **`WorkerDiscovery`** — the auto-discovery engine, exposed for custom use.
- **`TemporalMetricsInterceptor`** — records workflow-start counts/durations to
  a `System.Diagnostics.Metrics.Meter`.
- **`TemporalTestServerService`** — runs an in-process Temporal dev server when
  `Temporal:TestServer:Enabled` is `true`.

## Configuration

```json
{
  "Temporal": {
    "TargetHost": "localhost:7233",
    "Namespace": "default",
    "ApiKey": null,
    "Tls": null,
    "Metrics": {
      "Enabled": true,
      "MeterName": "Temporal.Hosting",
      "PrometheusBindAddress": null,
      "OpenTelemetryUrl": null
    },
    "TestServer": {
      "Enabled": true,
      "Port": 0
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
    .AddTemporalWorker("my-task-queue");

using var host = builder.Build();
await host.RunAsync();
```

### Auto-discovery and activity lifetimes

`AddTemporalWorker` scans the target assembly (default: the entry assembly)
once at registration time and registers every non-abstract `[Workflow]` class
and every class with an `[Activity]` method. Activity classes are registered as
`scoped` by default, and static classes as `static`. Override per type with the
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
builder.Services.AddTemporalWorker("my-task-queue", typeof(MyWorkflow), typeof(MyActivities));
```

### Worker versioning

Pass `WorkerDeploymentOptions` to opt into versioned workers (public preview):

```csharp
using Temporalio.Common;
using Temporalio.Worker;

builder.Services.AddTemporalWorker(
    "my-task-queue",
    new WorkerDeploymentOptions(new WorkerDeploymentVersion("my-app", "1.0"), useWorkerVersioning: true));
```

### Metrics

`Metrics:Enabled` registers a `System.Diagnostics.Metrics.Meter` and a client
interceptor that records workflow-start counts/durations. To also export the
SDK's runtime metrics, set either `Metrics:PrometheusBindAddress`
(e.g. `0.0.0.0:9000`) or `Metrics:OpenTelemetryUrl`
(e.g. `http://localhost:4317`), which configures the underlying
`TemporalRuntime` telemetry.

Not affiliated with or endorsed by Temporal Technologies.
