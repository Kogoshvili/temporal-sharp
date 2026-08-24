# POC: Kogoshvili.Temporal.Hosting — a "Spring Boot for .NET" worker starter

This is a proof-of-concept for a generic-host worker starter for the
[Temporal .NET SDK](https://github.com/temporalio/sdk-dotnet), built in
`src/Temporal.Hosting` with two runnable samples:

- `samples/Temporal.ConsoleWorker` — a plain `Microsoft.NET.Sdk` console app.
- `samples/Temporal.AspNetSample` — a minimal-API web app.

It is **not** published or part of the shipped `Kogoshvili.Temporal` tools — it is
a spike to explore what a richer, Spring Boot-style integration would look like.

---

## The problem

The .NET SDK already ships
[`Temporalio.Extensions.Hosting`](https://github.com/temporalio/sdk-dotnet/tree/main/src/Temporalio.Extensions.Hosting),
which gives you two thin building blocks:

- `AddTemporalClient(...)` — registers a lazy `ITemporalClient` singleton.
- `AddHostedTemporalWorker(...)` — registers a worker as an `IHostedService` and
  returns a builder for adding workflow/activity types.

What it does **not** give you is the ergonomics of the Java
[Spring Boot Temporal starter](https://docs.temporal.io/develop/java/integrations/spring-boot-integration):

- **Configuration binding** — connection options read straight from `appsettings.json`.
- **Auto-discovery** — the worker's `[Workflow]`/`[Activity]` types discovered by
  convention (`workers-auto-discovery` in Spring Boot).
- **Test-server toggle** — `spring.temporal.test-server.enabled` swaps the real
  connection for an in-memory test environment.
- **Metrics** — metrics wired up by default.

`Kogoshvili.Temporal.Hosting` layers those four conveniences on top of
`Temporalio.Extensions.Hosting`, so a developer gets the Java starter's experience
with a single line of DI configuration — in a console worker *or* a web host.

## Projects

- `src/Temporal.Hosting` — the starter library (`net8.0`, NuGet id
  `Kogoshvili.Temporal.Hosting`), referencing the real packages `Temporalio`,
  `Temporalio.Extensions.Hosting`, and `Microsoft.Extensions.Hosting`. It targets
  the plain .NET generic host — it does **not** require the ASP.NET Core shared
  framework (unlike the analyzer, which matches SDK types by name and never
  references them).
- `samples/Temporal.ConsoleWorker` — a `Host.CreateApplicationBuilder` console app
  that hosts one `[Workflow]` + one `[Activity]` and self-starts a workflow to
  prove the non-web path.
- `samples/Temporal.AspNetSample` — a minimal-API app that hosts the same and
  exposes an HTTP endpoint that starts the workflow.

## Public API

All extension methods live in `Microsoft.Extensions.DependencyInjection` (so they
are available as `services.AddTemporal(...)` / `builder.Services.AddTemporal(...)`
with no extra `using`). Options and builder types live in
`Kogoshvili.Temporal.Hosting`.

```csharp
using Kogoshvili.Temporal.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Generic host (console worker)...
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddTemporal(builder.Configuration);
builder.Services.AddTemporalWorker("my-task-queue");

// ...or the web host.
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddTemporal(builder.Configuration);
builder.Services.AddTemporalWorker("my-task-queue");
```

`AddTemporal` overloads:

```csharp
AddTemporal()                              // defaults (localhost:7233, "default")
AddTemporal(IConfiguration configuration)  // binds the "Temporal" section
AddTemporal(Action<TemporalOptions> configure)
AddTemporal(TemporalOptions options)
```

`AddTemporalWorker` overloads (on `TemporalBuilder` and `IServiceCollection`):

```csharp
AddTemporalWorker(
    string taskQueue,
    Assembly? assembly = null,                      // default: entry assembly
    Action<TemporalWorkerServiceOptions>? configure = null)
```

It returns the underlying `ITemporalWorkerServiceOptionsBuilder`, so the usual
hosting helpers still chain:

```csharp
builder.Services
    .AddTemporal(builder.Configuration)
    .AddTemporalWorker("my-task-queue", configure: o => o.MaxConcurrentActivities = 5)
    .AddWorkflow<ExtraWorkflow>();   // explicit, on top of what auto-discovery found
```

### Console worker startup

```csharp
using Kogoshvili.Temporal.ConsoleWorker;
using Kogoshvili.Temporal.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddTemporal(builder.Configuration)
    .AddTemporalWorker("console-queue");

builder.Services.AddHostedService<WorkerDemoService>(); // self-starts one workflow

using var host = builder.Build();
await host.RunAsync();
```

### Web host startup

```csharp
using Kogoshvili.Temporal.AspNetSample;
using Kogoshvili.Temporal.Hosting;
using Temporalio.Client;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddTemporal(builder.Configuration)
    .AddTemporalWorker("aspnet-sample");

var app = builder.Build();

app.MapPost("/start/{name}", async (string name, ITemporalClient client) =>
{
    var handle = await client.StartWorkflowAsync(
        (GreetingWorkflow wf) => wf.RunAsync(name),
        new() { Id = $"greeting-{Guid.NewGuid():N}", TaskQueue = "aspnet-sample" });
    return Results.Ok(new { WorkflowId = handle.Id, Greeting = await handle.GetResultAsync() });
});

app.Run();
```

## Configuration

The `Temporal` section of `appsettings.json` is bound to `TemporalOptions`:

```json
{
  "Temporal": {
    "TargetHost": "localhost:7233",
    "Namespace": "default",
    "ApiKey": null,
    "Tls": {
      "Disabled": false,
      "Domain": null,
      "ServerRootCACertPath": null,
      "ClientCertPath": null,
      "ClientPrivateKeyPath": null
    },
    "Metrics": {
      "Enabled": true,
      "MeterName": "Temporal.Hosting"
    },
    "TestServer": {
      "Enabled": true,
      "Port": 0
    }
  }
}
```

| Key | Meaning |
| --- | --- |
| `Temporal:TargetHost` | Temporal server `host:port`. |
| `Temporal:Namespace` | Namespace to use (default `default`). |
| `Temporal:ApiKey` | Optional bearer API key (implies TLS unless disabled). |
| `Temporal:Tls.*` | mTLS/server-CA settings; paths are read at startup. |
| `Temporal:Metrics:Enabled` | Register a `System.Diagnostics.Metrics` meter + interceptor. |
| `Temporal:Metrics:MeterName` | Meter name. |
| `Temporal:TestServer:Enabled` | Use an in-process dev server instead of a real connection. |
| `Temporal:TestServer:Port` | Dev-server port; `0` (default) picks an ephemeral free port. |

## Convention auto-discovery

`AddTemporalWorker(taskQueue)` scans the target assembly (default: the entry
assembly) once at registration time:

- **Workflows** — every non-abstract class marked `[Workflow]` is registered via
  `AddWorkflow(type)`.
- **Activities** — every class with at least one `[Activity]` method is registered:
  - **static** classes (including `static class` — which report
    `IsAbstract && IsSealed`) via `AddStaticActivities(type)`, and
  - **instance** classes via `AddScopedActivities(type)` (registered in DI *and*
    on the worker, so activity constructors can take injected dependencies).

This mirrors Spring Boot's `workers-auto-discovery`: add a `[Workflow]`/`[Activity]`
type anywhere in the app assembly and it is picked up with no explicit registration.

## Test-server toggle

When `Temporal:TestServer:Enabled` is `true`, `AddTemporal` does not call
`AddTemporalClient()` (the real-connection path). Instead it registers:

1. `TemporalTestServerService` — an `IHostedService` that calls
   `WorkflowEnvironment.StartLocalAsync(...)`. Its `StartAsync` awaits the server,
   and it is registered *before* any worker, so the dev server is listening before
   workers connect. `StopAsync` disposes the environment (shutting the dev server
   down).
2. A single shared `TemporalClientConnectOptions` instance, registered in DI and
   also handed to `TemporalClient.CreateLazy(...)` as the `ITemporalClient`.

**Ephemeral port (no fixed port).** With `Temporal:TestServer:Port = 0` (the
default), the dev server binds a random free port (`TargetHost = "127.0.0.1:0"`).
After `StartLocalAsync` returns, `TemporalTestServerService` writes the resolved
`host:port` back into the shared connect options:

```csharp
connectOptions.TargetHost = environment.Client.Connection.Options.TargetHost;
```

The lazy `ITemporalClient`'s connection reads `TargetHost` on first connect (after
the test server is up), so it connects to the ephemeral port. Setting a concrete
`Port` still works if you want a reproducible port. This removes the earlier
"fixed port" limitation entirely.

This is the .NET equivalent of `spring.temporal.test-server.enabled=true`: a
sample or test can run end-to-end with no external Temporal server, and without
risk of port collisions.

## Metrics

When `Temporal:Metrics:Enabled` is `true`:

- A `System.Diagnostics.Metrics.Meter` (named by `Temporal:Metrics:MeterName`) is
  registered in DI, available for app code to add its own instruments.
- `TemporalMetricsInterceptor` (an `IClientInterceptor`) is added to the client's
  interceptor chain and records, per workflow-start request:
  - `temporal.client.workflow.start.count` (counter, `workflows`), and
  - `temporal.client.workflow.start.duration` (histogram, `ms`).

It is wired onto both the real-connection path and the test-server path.

### Exporting SDK runtime metrics

Set `Temporal:Metrics:PrometheusBindAddress` (e.g. `0.0.0.0:9000`) or
`Temporal:Metrics:OpenTelemetryUrl` (e.g. `http://localhost:4317`) to additionally
configure the SDK `TemporalRuntime` to export its own metrics. This is separate
from the interceptor above, which records custom app-level workflow-start metrics.

## Activity lifetimes

Auto-discovered activity classes are registered as `scoped` by default and static
classes as `static`. Override per type with `[ActivityLifetime]`:

```csharp
[ActivityLifetime(ActivityLifetime.Singleton)]
public class MyActivities
{
    [Activity]
    public Task DoAsync() => Task.CompletedTask;
}
```

## Worker versioning

Pass `WorkerDeploymentOptions` (public preview) to `AddTemporalWorker` to opt into
versioned workers:

```csharp
builder.Services.AddTemporalWorker(
    "my-task-queue",
    new WorkerDeploymentOptions(new WorkerDeploymentVersion("my-app", "1.0"), useWorkerVersioning: true));
```

## Limitations / POC scope

- **No time-skipping test server** — only `WorkflowEnvironment.StartLocalAsync`
  (dev server) is wired; the `StartTimeSkippingAsync` path lives in
  `Kogoshvili.Temporal.Testing`, where it belongs (the time-skipping server is
  single-test-at-a-time and not thread safe).
- **No reconnect on live reload** — `TemporalOptions` is registered through
  `IOptions<TemporalOptions>` / `IOptionsMonitor<TemporalOptions>` so options
  reload, but the client connection itself is established once at startup.
- **Runtime metrics export is opt-in** — `TemporalMetricsInterceptor` records
  custom workflow-start metrics; the SDK's own metrics are only exported when
  Prometheus/OpenTelemetry is configured as described above.

There is now a unit-test project (`tests/Temporal.Hosting.Tests`) covering
discovery, DI registration, options binding, validation, and the test-server
service.

## Demo

Run `./demo.sh` — it builds the solution, then runs both samples in test-server
mode and shows a workflow executing end-to-end. Captured output:

```
$ ./demo.sh
==> Building the solution (Release)...
  Determining projects to restore...
  All projects are up-to-date for restore.
  Temporal.Analyzers -> .../src/Temporal.Analyzers/bin/Release/netstandard2.0/Temporal.Analyzers.dll
  Temporal.Hosting -> .../src/Temporal.Hosting/bin/Release/net8.0/Temporal.Hosting.dll
  Temporal.Cli -> .../src/Temporal.Cli/bin/Release/net8.0/Temporal.Cli.dll
  Temporal.Analyzers.Tests -> .../tests/Temporal.Analyzers.Tests/bin/Release/net8.0/Temporal.Analyzers.Tests.dll
  Temporal.ConsoleWorker -> .../samples/Temporal.ConsoleWorker/bin/Release/net8.0/Temporal.ConsoleWorker.dll
  Temporal.AspNetSample -> .../samples/Temporal.AspNetSample/bin/Release/net8.0/Temporal.AspNetSample.dll
  Temporal.Cli.Tests -> .../tests/Temporal.Cli.Tests/bin/Release/net8.0/Temporal.Cli.Tests.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:01.96

===== Demo 1: generic-host console worker (samples/Temporal.ConsoleWorker) =====
==> Starting the console worker (Temporal:TestServer:Enabled=true => in-process dev server)...
==> Waiting for the self-started workflow to complete...
    done after ~5s
==> Console worker log:
      Temporal test server started on 127.0.0.1:36659
      Workflow result: Hello from the console worker, console! (workflow id console-eaf9917be9394d85a7d6ebf2bf75cde9)

===== Demo 2: minimal API host (samples/Temporal.AspNetSample) =====
==> Starting the web API (test-server mode) on http://127.0.0.1:5080...
==> Waiting for the app (and its in-process Temporal test server) to come up...
    ready after ~2s

==> Test-server startup log:
      Starting Temporal test server on 127.0.0.1:0
      Temporal test server started on 127.0.0.1:38439

==> GET /
{"service":"Temporal.AspNetSample","taskQueue":"aspnet-sample"}

==> POST /start/World (starts the auto-discovered workflow and awaits its result)
{"workflowId":"greeting-e75bfc4dc73b4ccf9365b49b46e14caf","greeting":"Hello, World!"}

==> Done. Both samples shut down.
```

Note the ephemeral test-server ports (`36659`, `38439`) — both samples ran without
a fixed port, and both produced a real greeting end-to-end: the console worker
self-started a workflow, and the web API returned one via HTTP.

---

> Not affiliated with or endorsed by Temporal Technologies.
