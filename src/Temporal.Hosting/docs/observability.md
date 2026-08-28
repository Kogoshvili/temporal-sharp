# Observability: metrics, tracing, and Core log forwarding

## Metrics

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

`Metrics:UseDefaultInterceptor` (default `true`) wires the built-in interceptor
into the client and workers. Set it to `false` to keep the meter but record your
own metrics via the SDK's `Interceptors = [...]` option.

`Metrics:BaggageTagKeys` is an explicit allowlist of OpenTelemetry baggage keys
whose values are attached as `baggage.<key>` tags. It is empty by default to
avoid leaking arbitrary baggage into metric dimensions. On the worker side these
tags only appear when tracing is also enabled, because baggage is propagated and
restored by the tracing interceptor.

## Tracing

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

`Tracing:UseDefaultInterceptor` (default `true`) wires the built-in tracing
interceptor into the client and workers. Set it to `false` to install your own
via `Interceptors = [...]`. `Tracing:BaggageTagKeys` attaches allowlisted baggage
entries as `baggage.<key>` span attributes (client, workflow, and activity
spans).

## Core log forwarding

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
