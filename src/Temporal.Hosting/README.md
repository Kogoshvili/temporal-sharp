# Kogoshvili.Temporal.Hosting

A generic-host worker starter for the [Temporal](https://temporal.io) .NET SDK,
inspired by the Java Spring Boot Temporal starter. It layers configuration
binding, convention-based auto-discovery, a metrics interceptor, and a
test-server toggle on top of
[`Temporalio.Extensions.Hosting`](https://github.com/temporalio/sdk-dotnet/tree/main/src/Temporalio.Extensions.Hosting).

Unlike `Kogoshvili.Temporal.Analyzers`, this library references the **real**
[`Temporalio`](https://www.nuget.org/packages/Temporalio) SDK and targets
**net8.0**.

## Features

- **Worker registration** — `AddTemporalWorker` with explicit type registration
  or opt-in `AddDiscoveredTypes()`, per-queue tuning, worker versioning, and
  multi-namespace support. → [docs/worker-registration.md](docs/worker-registration.md)
- **Activity-options presets** — default/named `ActivityOptions` from config,
  resolved via the `ActivityOps` facade. → [docs/activity-options.md](docs/activity-options.md)
- **Workflow-options presets & ID conventions** — typed `IWorkflowOps` facade,
  child-workflow ops, and workflow settings. → [docs/workflow-ops.md](docs/workflow-ops.md)
- **Schedules** — idempotent schedule registration from config or code. → [docs/schedules.md](docs/schedules.md)
- **Search attributes** — idempotent search-attribute bootstrap. → [docs/search-attributes.md](docs/search-attributes.md)
- **Connection & TLS** — connection retry/wait, transport options, TLS sources
  (file/env/Key Vault/Secrets Manager). → [docs/connection.md](docs/connection.md)
- **Observability** — metrics, tracing, and Core log forwarding. → [docs/observability.md](docs/observability.md)
- **Health checks** — client/worker liveness checks. → [docs/health-checks.md](docs/health-checks.md)
- **Payload codecs & per-field secrets** — encryption, claim-check, and
  `Secret<T>` field encryption. → [docs/data-converter.md](docs/data-converter.md)

Full configuration reference (all keys in one JSON block). → [docs/configuration.md](docs/configuration.md)

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

Configuration binds from the `Temporal` section of `appsettings.json`, overridden
by `Temporal__*` environment variables. See
[docs/configuration.md](docs/configuration.md) for the full schema.

Not affiliated with or endorsed by Temporal Technologies.
