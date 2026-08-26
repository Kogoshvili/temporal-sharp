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
- **Activity-options presets** — `Temporal:ActivityOptions` defines a default
  and named `ActivityOptions` presets, resolved from workflows via the static
  `ActivityOptionsRegistry` (workflows cannot use DI).
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
        "MaxCachedWorkflows": 1000
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
      "Presets": {
        "long-running": {
          "ScheduleToCloseTimeout": "00:30:00",
          "HeartbeatTimeout": "00:01:00"
        }
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

Pass `WorkerDeploymentOptions` to opt into versioned workers (public preview):

```csharp
using Temporalio.Common;
using Temporalio.Worker;

builder.Services.AddTemporalWorker(
    "my-task-queue",
    new WorkerDeploymentOptions(new WorkerDeploymentVersion("my-app", "1.0"), useWorkerVersioning: true));
```

### Per-queue worker tuning

Tune each worker from `Temporal:Workers:<task-queue>`. Every knob is optional;
unset values leave the SDK default untouched. An explicit `configure` delegate
passed to `AddTemporalWorker` overrides the appsettings value.

| Key | SDK property |
| --- | --- |
| `MaxConcurrentActivities` | `TemporalWorkerOptions.MaxConcurrentActivities` |
| `MaxConcurrentWorkflowTasks` | `TemporalWorkerOptions.MaxConcurrentWorkflowTasks` |
| `MaxConcurrentLocalActivities` | `TemporalWorkerOptions.MaxConcurrentLocalActivities` |
| `MaxConcurrentActivityTaskPolls` | `TemporalWorkerOptions.MaxConcurrentActivityTaskPolls` |
| `MaxConcurrentWorkflowTaskPolls` | `TemporalWorkerOptions.MaxConcurrentWorkflowTaskPolls` |
| `GracefulShutdownTimeout` | `TemporalWorkerOptions.GracefulShutdownTimeout` |
| `MaxCachedWorkflows` | `TemporalWorkerOptions.MaxCachedWorkflows` |

### Activity-options presets

`Temporal:ActivityOptions` seeds a default and named `ActivityOptions` presets
(timeouts, retry policy, cancellation type, task queue). Workflows resolve them
through the static `ActivityOptionsRegistry` — workflows run in the replay
sandbox and cannot use DI, so the registry is populated once at `AddTemporal`
time and only read during execution:

```csharp
[Workflow]
public sealed class MyWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string name)
    {
        await Workflow.ExecuteActivityAsync(
            () => MyActivities.DoIt(name),
            ActivityOptionsRegistry.Get("long-running")); // or .GetDefault()

        return "done";
    }
}
```

Each preset must set `ScheduleToCloseTimeout` or `StartToCloseTimeout` (the
SDK's own rule); unset properties leave the SDK defaults, and an unset `Retry`
means "retry forever". Presets are captured at startup and are **not**
live-reloaded, to keep workflow replay deterministic.

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
